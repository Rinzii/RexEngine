using System.Runtime.CompilerServices;
using System.Text;
using bottlenoselabs.C2CS.Runtime;
using static Tracy.PInvoke;

using static global::Tracy.PInvoke;

namespace Rex.Shared.Profiling.Tracy;
/// <summary>
/// Defines the type of plot to be displayed in the Tracy profiler when using the <see cref="TracyProfiler.PlotConfig"/> method.
/// </summary>
public enum TracyPlotType
{
    /// <summary>
    /// values will be displayed as plain numbers 
    /// </summary>
    Number,

    /// <summary>
    /// treats the values as memory sizes. Will display kilobytes, megabytes, etc.
    /// </summary>
    Memory,

    /// <summary>
    /// values will be displayed as percentage (with value 100 being equal to 100%).
    /// </summary>
    Percentage
}

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
    private static TracyConfiguration _configuration = new();

    // ReSharper disable once CollectionNeverUpdated.Local
    private static readonly ConcurrentDictionary<TracySourceLocationData, ulong> SourceLocationCache = new();

    // See Tracy documentation for details on string caching (section 3.1)
    // ReSharper disable once CollectionNeverUpdated.Local
    private static readonly ConcurrentDictionary<string, CString> StringCache = new();
    private static readonly CString EmptyString = CString.FromString(string.Empty);

    /// <summary>
    /// Current configuration settings for the profiler. This can be updated at runtime by calling <see cref="EnableProfiler"/> with a new configuration instance.
    /// </summary>
    public static TracyConfiguration Configuration => Volatile.Read(ref _configuration);

    private static readonly Stopwatch CleanupStopwatch = Stopwatch.StartNew();

    /// <summary>
    /// Current configuration settings for the profiler. This can be updated at runtime by calling <see cref="EnableProfiler"/> with a new configuration instance.
    /// </summary>
    public static TracyConfiguration Configuration { get; private set; } = new();

    /// <summary>
    /// Marks the end of a frame for Tracy. Should be called once per frame after all zones have ended to allow Tracy to calculate frame times and display them in the profiler UI.
    /// </summary>
    /// <param name="name">Optional name for the frame mark. If provided, it will be displayed in the profiler UI alongside the frame timing information.</param>
    [Conditional("REX_TRACY")]
    public static void FrameMark(string? name = null)
    {
        if (!Configuration.Enabled)
        {
            return;
        }

        CString nameStr = string.IsNullOrEmpty(name) ? default : GetOrCreateCString(name);
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
    public static TracyProfilerScope Zone(
        string? zoneName = null,
        bool active = true,
        uint color = 0,
        string? text = null,
        [CallerLineNumber] int lineNumber = 0,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "")
    {
#if REX_TRACY
        if (!Configuration.Enabled)
        {
            return TracyProfilerScope.NoOp;
        }

        var srcLoc = GetOrAddSourceLocationName(
            new TracySourceLocationData(lineNumber, filePath, memberName, zoneName, color));

        TracyCZoneCtx context = TracyEmitZoneBeginAlloc(srcLoc, active ? 1 : 0);
        var profilerScope = new TracyProfilerScope(context);

        if (text is not null)
        {
            profilerScope.ZoneText(text);
        }

        return profilerScope;
#else
        return TracyProfilerScope.NoOp;
#endif
    }

    /// <summary>
    /// Enables the Tracy profiler with the specified configuration. If the profiler is already enabled, this will update the configuration settings.
    /// </summary>
    /// <param name="configuration">The configuration settings to apply to the Tracy profiler. This includes options for enabling/disabling profiling and configuring cache cleanup behavior.</param>
    [Conditional("REX_TRACY")]
    public static void EnableProfiler(TracyConfiguration configuration)
    {
        Volatile.Write(ref _configuration, configuration);
    }

    /// <summary>
    /// Disables the Tracy profiler and clears all cached data.
    /// </summary>
    [Conditional("REX_TRACY")]
    public static void DisableProfiler()
    {
        if (!Configuration.Enabled)
        {
            return;
        }

        Volatile.Write(ref _configuration, new TracyConfiguration { Enabled = false });

        foreach (CString cString in StringCache.Values)
        {
            cString.Dispose();
        }
        StringCache.Clear();

        SourceLocationCache.Clear();
    }

    /// <summary>
    /// Sends a custom message to the Tracy profiler.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="callstack">The number of call stack frames to capture and include with the message.</param>
    [Conditional("REX_TRACY")]
    public static void Message(string message, int callstack = 0)
    {
        if (!Configuration.Enabled || string.IsNullOrEmpty(message))
        {
            return;
        }

        var messageStr = CString.FromString(message);
        TracyEmitMessage(messageStr, (ulong)Encoding.UTF8.GetByteCount(message), callstack);
    }

    /// <summary>
    /// Sends application-specific information to the profiler.
    /// </summary>
    /// <param name="info">The application information to send.</param>
    [Conditional("REX_TRACY")]
    public static void MessageAppInfo(string info)
    {
        if (!Configuration.Enabled || string.IsNullOrEmpty(info))
        {
            return;
        }

        var infoStr = CString.FromString(info);
        TracyEmitMessageAppinfo(infoStr, (ulong)Encoding.UTF8.GetByteCount(info));
    }

    /// <summary>
    /// Sets the name of the current thread in the profiler.
    /// </summary>
    /// <param name="name">The name to set for the current thread.</param>
    [Conditional("REX_TRACY")]
    public static void SetThreadName(string name)
    {
        if (!Configuration.Enabled || string.IsNullOrEmpty(name))
        {
            return;
        }

        TracySetThreadName(GetOrCreateCString(name));
    }

    /// <summary>
    /// Configures a plot in the profiler with the specified parameters. This should be called before emitting any values for the plot using <see cref="PlotFloat"/>, <see cref="PlotInt"/> or <see cref="Plot"/>.
    /// </summary>
    /// <param name="name">The name of the plot to configure. This will be used to identify the plot in the profiler UI and should be unique for each plot.</param>
    /// <param name="type">The type of the plot, which determines how values will be displayed in the profiler UI. See <see cref="TracyPlotType"/> for available options.</param>
    /// <param name="step">Whether to display the plot as a step graph in the profiler UI. If true, values will be connected with horizontal and vertical lines to create a step-like appearance, while if false, values will be connected with straight lines.</param>
    /// <param name="fill">Whether to fill the area under the plot line in the profiler UI. If true, the area under the line will be filled with color, while if false, only the line itself will be displayed.</param>
    /// <param name="color"></param>
    [Conditional("REX_TRACY")]
    public static void PlotConfig(string name, TracyPlotType type = TracyPlotType.Number, bool step = false, bool fill = true, uint color = 0)
    {
        if (!Configuration.Enabled || string.IsNullOrEmpty(name))
        {
            return;
        }

        TracyEmitPlotConfig(GetOrCreateCString(name), (int)type, step ? 1 : 0, fill ? 1 : 0, color);
    }

    /// <summary>
    /// Emits a plot value to the profiler for the specified plot name.
    /// </summary>
    /// <param name="name">The name of the plot to emit the value for. This should match the name used when configuring the plot with <see cref="PlotConfig"/>.</param>
    /// <param name="value">The value to emit for the plot.</param>
    [Conditional("REX_TRACY")]
    public static void PlotFloat(string name, float value)
    {
        if (!Configuration.Enabled || string.IsNullOrEmpty(name))
        {
            return;
        }

        TracyEmitPlotFloat(GetOrCreateCString(name), value);
    }

    /// <summary>
    /// Emits a plot value to the profiler for the specified plot name.
    /// </summary>
    /// <param name="name">The name of the plot to emit the value for. This should match the name used when configuring the plot with <see cref="PlotConfig"/>.</param>
    /// <param name="value">The value to emit for the plot.</param>
    [Conditional("REX_TRACY")]
    public static void PlotInt(string name, long value)
    {
        if (!Configuration.Enabled || string.IsNullOrEmpty(name))
        {
            return;
        }

        TracyEmitPlotInt(GetOrCreateCString(name), value);
    }

    /// <summary>
    /// Emits a plot value to the profiler for the specified plot name.
    /// </summary>
    /// <param name="name">The name of the plot to emit the value for. This should match the name used when configuring the plot with <see cref="PlotConfig"/>.</param>
    /// <param name="value">The value to emit for the plot.</param>
    [Conditional("REX_TRACY")]
    public static void Plot(string name, double value)
    {
        if (!Configuration.Enabled || string.IsNullOrEmpty(name))
        {
            return;
        }

        TracyEmitPlot(GetOrCreateCString(name), value);
    }

    // ReSharper disable once UnusedParameter.Local
    private static CString GetOrCreateCString(string str)
    {
#if REX_TRACY
        if (!Configuration.Enabled || string.IsNullOrEmpty(str))
        {
            return EmptyString;
        }

        return StringCache.GetOrAdd(str, CString.FromString);
#else
        return EmptyString;
#endif
    }

#if REX_TRACY
    private static ulong GetOrAddSourceLocationName(TracySourceLocationData key)
    {
        var srcLoc = SourceLocationCache.GetOrAdd(
            key,
            static key =>
            {
                var fileStr = GetOrCreateCString(key.FilePath);
                var memberStr = GetOrCreateCString(key.MemberName);
                var zoneStr = key.ZoneName is not null ? GetOrCreateCString(key.ZoneName) : default;

                return TracyAllocSrclocName(
                    (uint)key.LineNumber,
                    fileStr,
                    (ulong)Encoding.UTF8.GetByteCount(key.FilePath),
                    memberStr,
                    (ulong)Encoding.UTF8.GetByteCount(key.MemberName),
                    zoneStr,
                    (ulong)(key.ZoneName is not null ? Encoding.UTF8.GetByteCount(key.ZoneName) : 0),
                    key.Color);
            });

        return srcLoc;
    }
#endif

    private readonly record struct TracySourceLocationData(
        int LineNumber,
        string FilePath,
        string MemberName,
        string? ZoneName,
        uint Color);
}
