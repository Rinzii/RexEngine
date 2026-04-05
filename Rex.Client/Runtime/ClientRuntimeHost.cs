using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Rex.Client.Graphics;
using Rex.Shared.Timing;

namespace Rex.Client.Runtime;

/// <summary>
/// Tunables for <see cref="ClientRuntimeHost"/>.
/// </summary>
public sealed class ClientRuntimeOptions
{
    /// <summary>
    /// Fixed simulation steps per second shared with the tick clock.
    /// </summary>
    public int TickRate { get; init; }

    /// <summary>
    /// Skips window creation and presentation when true.
    /// </summary>
    public bool Headless { get; set; }

    /// <summary>
    /// Initial window title when not headless.
    /// </summary>
    public string WindowTitle { get; set; } = "Rex Client";

    /// <summary>
    /// Initial window width in pixels.
    /// </summary>
    public int WindowWidth { get; set; } = 1280;

    /// <summary>
    /// Initial window height in pixels.
    /// </summary>
    public int WindowHeight { get; set; } = 720;
}

/// <summary>
/// Client main loop. Owns optional windowing, drains fixed ticks through <see cref="OnFixedUpdate"/>, then runs
/// variable frame callbacks through <see cref="OnUpdate"/> and <see cref="OnLateUpdate"/> before <see cref="OnRender"/>.
/// </summary>
public sealed partial class ClientRuntimeHost : IDisposable
{
    private readonly ClientRuntimeOptions _options;
    private readonly ILogger _logger;
    private readonly TickClock _clock;
    private readonly DeltaTimeSmoother _deltaSmoother = new();
    private bool _isRunning;
    private bool _disposed;

    /// <summary>
    /// Captures options and prepares logging for the frame loop.
    /// </summary>
    /// <param name="options">Headless flag dimensions and tick rate.</param>
    /// <param name="loggerFactory">Factory used to create the host category logger.</param>
    public ClientRuntimeHost(ClientRuntimeOptions options, ILoggerFactory loggerFactory)
    {
        _options = options;
        _logger = loggerFactory.CreateLogger<ClientRuntimeHost>();
        _clock = new TickClock(options.TickRate);
    }

    /// <summary>
    /// Options captured at construction.
    /// </summary>
    public ClientRuntimeOptions Options => _options;

    /// <summary>
    /// Simulation clock advanced by the main loop.
    /// </summary>
    public TickClock Clock => _clock;

    /// <summary>
    /// True while <see cref="Run"/> is inside its main loop.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Multiplier applied to frame deltas inside <see cref="FrameContext"/>.
    /// </summary>
    public float TimeScale { get; set; } = 1f;
    /// <summary>
    /// Optional window backend resolved by the startup layer.
    /// </summary>
    public IGameWindow? Window { get; set; }
    /// <summary>Invoked once at the start of <see cref="Run"/> before the main loop.</summary>
    public Action? OnInitialize { get; set; }

    /// <summary>Invoked zero or more times per frame for fixed simulation steps.</summary>
    public Action? OnFixedUpdate { get; set; }

    /// <summary>Invoked each frame after fixed steps and receives timing context.</summary>
    public Action<FrameContext>? OnUpdate { get; set; }

    /// <summary>Invoked each frame after <see cref="OnUpdate"/>.</summary>
    public Action<FrameContext>? OnLateUpdate { get; set; }

    /// <summary>Invoked each frame after <see cref="OnLateUpdate"/> for GPU work.</summary>
    public Action<FrameContext>? OnRender { get; set; }

    /// <summary>Invoked once when <see cref="Run"/> finishes for any reason.</summary>
    public Action? OnShutdown { get; set; }

    /// <summary>
    /// Enters the main loop until <see cref="Stop"/>, a closed window or a canceled <paramref name="cancellationToken"/> ends it.
    /// </summary>
    /// <param name="cancellationToken">Stops the loop when canceled.</param>
    public void Run(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        PrepareWindow();

        try
        {
            OnInitialize?.Invoke();
            RunMainLoop(cancellationToken);
        }
        finally
        {
            _isRunning = false;

            try
            {
                OnShutdown?.Invoke();
            }
            finally
            {
                if (!_options.Headless)
                {
                    Window?.Close();
                }
            }
        }
    }

    /// <summary>
    /// Requests shutdown for the client loop.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
    }

    /// <summary>
    /// Releases owned runtime resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Window?.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Opens a provided window backend when one exists.
    /// </summary>
    private void PrepareWindow()
    {
        if (_options.Headless)
        {
            return;
        }

        var window = Window;
        if (window == null)
        {
            LogWindowBackendUnavailable();
            return;
        }

        if (window.IsOpen)
        {
            return;
        }

        // A caller can register a backend later without changing the loop contract.
        window.Open(_options.WindowTitle, _options.WindowWidth, _options.WindowHeight);
    }

    private void RunMainLoop(CancellationToken cancellationToken)
    {
        _isRunning = true;
        var stopwatch = Stopwatch.StartNew();
        var previousTime = stopwatch.Elapsed.TotalSeconds;
        double accumulator = 0;
        ulong frameIndex = 0;

        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            var currentTime = stopwatch.Elapsed.TotalSeconds;
            var frameTime = currentTime - previousTime;
            previousTime = currentTime;

            // Drain wall time into fixed ticks, then mirror the leftover interval as alpha on the tick clock.
            var fixedSteps = PhasedLoop.RunFixedSteps(_clock, ref accumulator, frameTime, () => OnFixedUpdate?.Invoke());
            var alpha = (float)(accumulator / _clock.TickInterval);
            _clock.SetAlpha(alpha);
            frameIndex++;

            // Variable phase timing with hitch clamping plus a smoothed delta for stable motion or UI.
            var unscaledDt = Math.Min((float)frameTime, PhasedLoop.DefaultMaxFrameSeconds);
            var smoothDt = _deltaSmoother.Next(unscaledDt);
            var ctx = new FrameContext(
                _clock,
                unscaledDt,
                smoothDt,
                TimeScale,
                fixedSteps,
                alpha,
                frameIndex,
                stopwatch.Elapsed.TotalSeconds);

            if (!_options.Headless)
            {
                var window = Window;
                window?.PollEvents();
                if (window is { IsOpen: false })
                {
                    Stop();
                    break;
                }
            }

            InvokeUpdateCallback(OnUpdate, ctx, LogOnUpdateFailed);
            InvokeUpdateCallback(OnLateUpdate, ctx, LogOnLateUpdateFailed);
            OnRender?.Invoke(ctx);

            if (!_options.Headless)
            {
                Window?.SwapBuffers();
            }
            else
            {
                // Headless clients still need a scheduling point without blocking on vsync.
                Thread.Yield();
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            LogMainLoopCancellationRequested();
        }
    }

    /// <summary>Invokes a frame callback and logs failures without tearing down the loop.</summary>
    private static void InvokeUpdateCallback(Action<FrameContext>? callback, FrameContext ctx, Action<Exception> logFailure)
    {
        if (callback == null)
        {
            return;
        }

        try
        {
            callback(ctx);
        }
        catch (Exception ex)
        {
            logFailure(ex);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

public sealed partial class ClientRuntimeHost
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "OnUpdate threw an exception.")]
    private partial void LogOnUpdateFailed(Exception ex);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "OnLateUpdate threw an exception.")]
    private partial void LogOnLateUpdateFailed(Exception ex);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Main loop exiting due to cancellation.")]
    private partial void LogMainLoopCancellationRequested();

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "No window backend is registered so the client will stay headless.")]
    private partial void LogWindowBackendUnavailable();
}
