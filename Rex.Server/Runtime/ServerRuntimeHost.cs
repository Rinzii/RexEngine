using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Rex.Shared.Profiling.Tracy;
using Rex.Shared.Timing;

namespace Rex.Server.Runtime;

/// <summary>
/// Tunables for <see cref="ServerRuntimeHost"/>.
/// </summary>
public sealed class ServerRuntimeOptions
{
    /// <summary>
    /// Fixed simulation steps per second.
    /// </summary>
    public int TickRate { get; init; }
}

/// <summary>
/// Dedicated server main loop. Owns a <see cref="TickClock"/> and runs fixed time step simulation through
/// <see cref="OnFixedUpdate"/> plus variable frame callbacks through <see cref="OnUpdate"/> and <see cref="OnLateUpdate"/>.
/// Game or sandbox code wires <see cref="OnInitialize"/> and <see cref="OnShutdown"/> for startup and teardown.
/// Engine entry <see cref="Rex.Server.Startup.GameServerStart"/> resolves this host and calls <see cref="Run"/>.
/// </summary>
public sealed partial class ServerRuntimeHost : IDisposable
{
    private readonly ILogger _logger;
    private readonly DeltaTimeSmoother _deltaSmoother = new();
    private bool _isRunning;
    private bool _disposed;

    /// <summary>
    /// Builds the tick clock and logger used by the main loop.
    /// </summary>
    /// <param name="options">Tick rate and future host tunables.</param>
    /// <param name="loggerFactory">Factory used to create the host category logger.</param>
    public ServerRuntimeHost(ServerRuntimeOptions options, ILoggerFactory loggerFactory)
    {
        Options = options;
        _logger = loggerFactory.CreateLogger<ServerRuntimeHost>();
        Clock = new TickClock(options.TickRate);
    }

    /// <summary>
    /// Options supplied at construction.
    /// </summary>
    public ServerRuntimeOptions Options { get; }

    /// <summary>
    /// Simulation clock. Tick index and interpolation alpha update each loop iteration.
    /// </summary>
    public TickClock Clock { get; }

    /// <summary>
    /// True while <see cref="Run"/> is inside its main loop.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Multiplier applied to frame deltas inside <see cref="FrameContext"/>.
    /// </summary>
    public float TimeScale { get; set; } = 1f;

    /// <summary>Invoked once at the start of <see cref="Run"/> before the main loop.</summary>
    public Action? OnInitialize { get; set; }

    /// <summary>Invoked zero or more times per frame for fixed simulation steps.</summary>
    public Action? OnFixedUpdate { get; set; }

    /// <summary>Invoked each frame after fixed steps and receives timing context.</summary>
    public Action<FrameContext>? OnUpdate { get; set; }

    /// <summary>Invoked each frame after <see cref="OnUpdate"/>.</summary>
    public Action<FrameContext>? OnLateUpdate { get; set; }

    /// <summary>Invoked when the loop exits, including after <see cref="Stop"/> or cancellation.</summary>
    public Action? OnShutdown { get; set; }

    /// <summary>
    /// Enters the main loop until <see cref="Stop"/>, disposal or a canceled <paramref name="cancellationToken"/> ends it.
    /// </summary>
    /// <param name="cancellationToken">Stops the loop when canceled.</param>
    public void Run(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            OnInitialize?.Invoke();
            RunMainLoop(cancellationToken);
        }
        finally
        {
            _isRunning = false;
            OnShutdown?.Invoke();
        }
    }

    /// <summary>
    /// Requests the main loop to exit. <see cref="OnShutdown"/> runs before <see cref="Run"/> returns.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
    }

    /// <summary>
    /// Marks the host disposed so a subsequent <see cref="Run"/> throws.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
    }

    /// <summary>
    /// Wall clock loop that advances fixed steps, builds <see cref="FrameContext"/> and yields the thread between frames.
    /// </summary>
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
            using var _ = TracyProfiler.BeginZone("MainLoop", true, 0xFF5A00);

            var currentTime = stopwatch.Elapsed.TotalSeconds;
            var frameTime = currentTime - previousTime;
            previousTime = currentTime;

            // Drain wall time into fixed ticks, then expose the fractional remainder as alpha for blending reads.
            var fixedSteps = PhasedLoop.RunFixedSteps(Clock, ref accumulator, frameTime, () => OnFixedUpdate?.Invoke());
            var alpha = (float)(accumulator / Clock.TickInterval);
            Clock.SetAlpha(alpha);
            frameIndex++;

            // Variable phase timing. Clamp hitches for gameplay deltas and smooth the same value for cameras or UI.
            var unscaledDt = Math.Min((float)frameTime, PhasedLoop.DefaultMaxFrameSeconds);
            var smoothDt = _deltaSmoother.Next(unscaledDt);
            var ctx = new FrameContext(
                Clock,
                unscaledDt,
                smoothDt,
                TimeScale,
                fixedSteps,
                alpha,
                frameIndex,
                stopwatch.Elapsed.TotalSeconds);

            InvokeUpdateCallback(OnUpdate, ctx, LogOnUpdateFailed);
            InvokeUpdateCallback(OnLateUpdate, ctx, LogOnLateUpdateFailed);
            // No swap chain on the dedicated server. Yield so an uncapped loop does not burn a full core.
            Thread.Yield();

            TracyProfiler.MarkFrameCompleted();
            Thread.Sleep(1);
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

public sealed partial class ServerRuntimeHost
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "OnUpdate threw an exception.")]
    private partial void LogOnUpdateFailed(Exception ex);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "OnLateUpdate threw an exception.")]
    private partial void LogOnLateUpdateFailed(Exception ex);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Main loop exiting due to cancellation.")]
    private partial void LogMainLoopCancellationRequested();
}
