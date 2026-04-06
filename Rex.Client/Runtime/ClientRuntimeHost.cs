using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Rex.Client.Graphics;
using Rex.Shared.Profiling.Tracy;
using Rex.Shared.Timing;

namespace Rex.Client.Runtime;

/// <summary>
/// Options that drive the client runtime loop.
/// </summary>
public sealed class ClientRuntimeOptions
{
    public int TickRate { get; init; }
    public bool Headless { get; set; }
    public string WindowTitle { get; set; } = "Rex Client";
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 720;
}

/// <summary>
/// Runs the reusable client frame loop.
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
    /// Creates a runtime host for a client process.
    /// </summary>
    public ClientRuntimeHost(ClientRuntimeOptions options, ILoggerFactory loggerFactory)
    {
        _options = options;
        _logger = loggerFactory.CreateLogger<ClientRuntimeHost>();
        _clock = new TickClock(options.TickRate);
    }

    public ClientRuntimeOptions Options => _options;
    public TickClock Clock => _clock;
    public bool IsRunning => _isRunning;
    public float TimeScale { get; set; } = 1f;
    /// <summary>
    /// Optional window backend resolved by the startup layer.
    /// </summary>
    public IGameWindow? Window { get; set; }
    public Action? OnInitialize { get; set; }
    public Action? OnFixedUpdate { get; set; }
    public Action<FrameContext>? OnUpdate { get; set; }
    public Action<FrameContext>? OnLateUpdate { get; set; }
    public Action<FrameContext>? OnRender { get; set; }
    public Action? OnShutdown { get; set; }

    /// <summary>
    /// Runs the client loop until stop or cancellation.
    /// </summary>
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
            // TODO (xLuxy): This is for testing only and should be removed once we have fully integrated Tracy
            using var _ = TracyProfiler.BeginZone("MainLoop",true, 0xFF5A00);
            
            var currentTime = stopwatch.Elapsed.TotalSeconds;
            var frameTime = currentTime - previousTime;
            previousTime = currentTime;

            var fixedSteps = PhasedLoop.RunFixedSteps(_clock, ref accumulator, frameTime, () => OnFixedUpdate?.Invoke());
            var alpha = (float)(accumulator / _clock.TickInterval);
            _clock.SetAlpha(alpha);
            frameIndex++;

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
                Thread.Yield();
            }
            
            TracyProfiler.MarkFrameCompleted();
            Thread.Sleep(1);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            LogMainLoopCancellationRequested();
        }
    }

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
