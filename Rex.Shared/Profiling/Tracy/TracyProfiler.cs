using System.Collections.Concurrent;
using System.Diagnostics;
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

    /// <summary>
    /// Interval at which to clean up unused cached C strings. This helps prevent unbounded memory growth from caching C strings for source locations and zone names. The cleanup process will remove any cached entries that have not been accessed within the last <see cref="CStringCacheEntryLifetimeSeconds"/> seconds.
    /// </summary>
    public TimeSpan CStringCacheCleanupInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Lifetime in seconds for cached C string entries. Cached entries that have not been accessed within this time frame will be removed during the cleanup process.
    /// </summary>
    public uint CStringCacheEntryLifetimeSeconds { get; set; } = 60;
}

/// <summary>
/// Static helper to interact with the Tracy profiler.
/// </summary>
public static class TracyProfiler
{
    private static ConcurrentDictionary<string, CStringCacheEntry> CStringCache { get; } = new();
    private static ConcurrentDictionary<SourceLocationKey, ulong> SourceLocationCache { get; } = new();

    private static readonly Stopwatch CleanupStopwatch = Stopwatch.StartNew();

    /// <summary>
    /// Current configuration settings for the profiler. This can be updated at runtime by calling <see cref="EnableProfiler"/> with a new configuration instance.
    /// </summary>
    public static TracyConfiguration Configuration { get; private set; } = new();

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

        var nameStr = name?.Length > 0 ? GetOrCreateCString(name) : null;
        TracyEmitFrameMark(nameStr);

        CheckAndCleanupCStringCache();

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

        var key = new SourceLocationKey(filePath, lineNumber, memberName, zoneName);
        var srcLoc = SourceLocationCache.GetOrAdd(key, static k =>
        {
            var sourceStr = GetOrCreateCString(k.FilePath);
            var funcStr = GetOrCreateCString(k.MemberName);
            var zoneNameStr = k.ZoneName != null ? GetOrCreateCString(k.ZoneName) : null;

            return TracyAllocSrclocName(
                (uint)k.LineNumber,
                sourceStr,
                (ulong)k.FilePath.Length,
                funcStr,
                (ulong)k.MemberName.Length,
                zoneNameStr,
                (ulong)(k.ZoneName?.Length ?? 0));
        });

        var context = TracyEmitZoneBeginAlloc(srcLoc, active ? 1 : 0);
        var profilerScope = new TracyProfilerScope(context);

        if (text is not null)
        {
            profilerScope.SetText(text);
        }

        profilerScope.SetColor(color);

        return profilerScope;
    }

    /// <summary>
    /// Enables the Tracy profiler with the specified configuration. If the profiler is already enabled, this will update the configuration settings.
    /// </summary>
    /// <param name="configuration">The configuration settings to apply to the Tracy profiler. This includes options for enabling/disabling profiling and configuring cache cleanup behavior.</param>
    public static void EnableProfiler(TracyConfiguration configuration)
    {
        Configuration = configuration;
    }

    /// <summary>
    /// Disables the Tracy profiler and clears all cached data.
    /// </summary>
    public static void DisableProfiler()
    {
        Configuration.Enabled = false;

        foreach (var entry in CStringCache.Values)
        {
            entry.Value.Dispose();
        }

        CStringCache.Clear();
        SourceLocationCache.Clear();
    }

    private static CString GetOrCreateCString(string value)
    {
        if (CStringCache.TryGetValue(value, out var entry))
        {
            entry.LastAccessedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return entry.Value;
        }

        var cString = new CString(value);
        var newEntry = new CStringCacheEntry(cString, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        CStringCache.TryAdd(value, newEntry);
        return cString;
    }

    private static void CheckAndCleanupCStringCache()
    {
        if (CleanupStopwatch.Elapsed < Configuration.CStringCacheCleanupInterval)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var threshold = now - Configuration.CStringCacheEntryLifetimeSeconds;

        foreach (var kvp in CStringCache)
        {
            if (kvp.Value.LastAccessedTimestamp >= threshold)
            {
                continue;
            }

            if (CStringCache.TryRemove(kvp.Key, out var removed))
            {
                removed.Value.Dispose();
            }
        }

        CleanupStopwatch.Restart();
    }

    private record struct CStringCacheEntry(CString Value, long LastAccessedTimestamp);

    private record struct SourceLocationKey(string FilePath, int LineNumber, string MemberName, string? ZoneName);
}