using System.Runtime.CompilerServices;
using bottlenoselabs.C2CS.Runtime;

namespace Rex.Shared.Profiling.Tracy;

using static global::Tracy.PInvoke;

/// <summary>
/// Configuration settings for the Tracy profiler.
/// </summary>
public sealed class TracyConfiguration
{
    /// <summary>
    /// Whether to enable the Tracy profiler and collect profiling data.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// Static helper to interact with the Tracy profiler.
/// </summary>
public static class TracyProfiler
{
    private static TracyConfiguration _configuration = new() { Enabled = false };

    /// <summary>
    /// Current configuration settings for the profiler. This can be updated at runtime by calling <see cref="EnableProfiler"/> with a new configuration instance.
    /// </summary>
    public static TracyConfiguration Configuration => Volatile.Read(ref _configuration);

    /// <summary>
    /// Marks the end of a frame for Tracy. Should be called once per frame after all zones have ended to allow Tracy to calculate frame times and display them in the profiler UI.
    /// </summary>
    /// <param name="name">Optional name for the frame mark. If provided, it will be displayed in the profiler UI alongside the frame timing information.</param>
    public static void MarkFrameCompleted(string? name = null)
    {
        if (!Configuration.Enabled)
        {
            return;
        }

        var nameStr = name?.Length > 0 ? CString.FromString(name) : null;
        TracyEmitFrameMark(nameStr);

        // FIXME (xLuxy): This is required for now while using Tracy - see https://github.com/Rinzii/RexEngine/issues/19
        Thread.Sleep(1);
    }

    /// <summary>
    /// Begins a profiling zone with the specified parameters. Returns a <see cref="TracyProfilerScope"/> that will automatically end the zone when disposed. If profiling is disabled, returns null.
    /// </summary>
    /// <param name="zoneName">Optional name for the zone. If provided, it will be displayed in the profiler UI to help identify the zone.</param>
    /// <param name="active">Whether the zone is active. If false, the zone will be ignored by the profiler and will not contribute to profiling data.</param>
    /// <param name="color">Optional color for the zone in the profiler UI, specified as a 32-bit unsigned integer in ARGB format. If not provided, a default color will be used.</param>
    /// <param name="text">Optional text to display in the profiler UI when hovering over the zone. If provided, it will be shown as a tooltip to provide additional context about the zone.</param>
    /// <param name="lineNumber">Automatically captured line number of the caller. Used for caching source location information in the profiler.</param>
    /// <param name="filePath">Automatically captured file path of the caller. Used for caching source location information in the profiler.</param>
    /// <param name="memberName">Automatically captured member name of the caller. Used for caching source location information in the profiler.</param>
    /// <returns>A <see cref="TracyProfilerScope"/> that will end the zone when disposed, or null if profiling is disabled.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TracyProfilerScope? BeginZone(
        string? zoneName = null,
        bool active = true,
        uint color = 0,
        string? text = null,
        [CallerLineNumber] int lineNumber = 0,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "")
    {
        if (!Configuration.Enabled)
        {
            return null;
        }

        var sourceStr = CString.FromString(filePath);
        var funcStr = CString.FromString(memberName);
        var zoneNameStr = zoneName != null ? CString.FromString(zoneName) : null;

        var srcLoc = TracyAllocSrclocName(
            (uint)lineNumber,
            sourceStr,
            (ulong)filePath.Length,
            funcStr,
            (ulong)memberName.Length,
            zoneNameStr,
            (ulong)(zoneName?.Length ?? 0),
            color);

        var context = TracyEmitZoneBeginAlloc(srcLoc, active ? 1 : 0);
        var profilerScope = new TracyProfilerScope(context);

        if (text is not null)
        {
            profilerScope.SetText(text);
        }

        return profilerScope;
    }

    /// <summary>
    /// Enables the Tracy profiler with the specified configuration. If the profiler is already enabled, this will update the configuration settings.
    /// </summary>
    /// <param name="configuration">The configuration settings to apply to the Tracy profiler. This includes options for enabling/disabling profiling and configuring cache cleanup behavior.</param>
    public static void EnableProfiler(TracyConfiguration configuration)
    {
        Volatile.Write(ref _configuration, configuration);
    }

    /// <summary>
    /// Disables the Tracy profiler and clears all cached data.
    /// </summary>
    public static void DisableProfiler()
    {
        Volatile.Write(ref _configuration, new TracyConfiguration { Enabled = false });
    }
}
