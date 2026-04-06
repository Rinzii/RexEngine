using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using bottlenoselabs.C2CS.Runtime;

namespace Rex.Shared.Profiling.Tracy;

using static global::Tracy.PInvoke;

public static class TracyProfiler
{
    public static bool ProfilerEnabled { get; } = true;

    private record struct CStringCacheEntry(CString Value, long LastAccessedTimestamp);
    private record struct SourceLocationKey(string FilePath, int LineNumber, string MemberName, string? ZoneName);

    private static ConcurrentDictionary<string, CStringCacheEntry> CStringCache { get; } = new();
    private static ConcurrentDictionary<SourceLocationKey, ulong> SourceLocationCache { get; } = new();

    public static void MarkFrameCompleted(string? name = null)
    {
        if (!ProfilerEnabled)
        {
            return;
        }

        var nameStr = name?.Length > 0 ? GetOrCreateCString(name) : null;
        TracyEmitFrameMark(nameStr);
    }

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
        if (!ProfilerEnabled)
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
}
