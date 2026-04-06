using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Rex.Shared.Profiling.Tracy;
using Rex.Shared.Timing;

namespace Rex.Server.Runtime;

public sealed class ServerRuntimeOptions
{
    public int TickRate { get; init; }
}

public sealed partial class ServerRuntimeHost : IDisposable
{
    private readonly ServerRuntimeOptions _options;
    private readonly ILogger _logger;
    private readonly TickClock _clock;
    private readonly DeltaTimeSmoother _deltaSmoother = new();
    private bool _isRunning;
    private bool _disposed;

    public ServerRuntimeHost(ServerRuntimeOptions options, ILoggerFactory loggerFactory)
    {
        _options = options;
        _logger = loggerFactory.CreateLogger<ServerRuntimeHost>();
        _clock = new TickClock(options.TickRate);
    }

    public ServerRuntimeOptions Options => _options;
    public TickClock Clock => _clock;
    public bool IsRunning => _isRunning;
    public float TimeScale { get; set; } = 1f;
    public Action? OnInitialize { get; set; }
    public Action? OnFixedUpdate { get; set; }
    public Action<FrameContext>? OnUpdate { get; set; }
    public Action<FrameContext>? OnLateUpdate { get; set; }
    public Action? OnShutdown { get; set; }

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

    public void Stop()
    {
        _isRunning = false;
    }

    public void Dispose()
    {
        _disposed = true;
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
            using var _ = TracyProfiler.BeginZone("MainLoop", true, 0xFF5A00);

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

            InvokeUpdateCallback(OnUpdate, ctx, LogOnUpdateFailed);
            InvokeUpdateCallback(OnLateUpdate, ctx, LogOnLateUpdateFailed);
            Thread.Yield();

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

public sealed partial class ServerRuntimeHost
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "OnUpdate threw an exception.")]
    private partial void LogOnUpdateFailed(Exception ex);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "OnLateUpdate threw an exception.")]
    private partial void LogOnLateUpdateFailed(Exception ex);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Main loop exiting due to cancellation.")]
    private partial void LogMainLoopCancellationRequested();
}
