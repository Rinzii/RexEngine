using System.Diagnostics;
using System.Text;
using bottlenoselabs.C2CS.Runtime;

using static global::Tracy.PInvoke;

namespace Rex.Shared.Profiling.Tracy;
/// <summary>
/// Represents a profiling scope for the Tracy profiler.
/// </summary>
public readonly struct TracyProfilerScope : IDisposable
{
    private readonly TracyCZoneCtx? _context;

    /// <summary>
    /// A static instance of <see cref="TracyProfilerScope"/> that represents a no-op scope which will be used when profiling is disabled
    /// </summary>
    public static TracyProfilerScope NoOp => new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TracyProfilerScope"/> struct with default values. This constructor is used to create an empty scope that does not correspond to any active Tracy zone.
    /// </summary>
    public TracyProfilerScope()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TracyProfilerScope"/> struct with the specified Tracy zone context.
    /// </summary>
    /// <param name="context">The Tracy zone context associated with this scope.</param>
    public TracyProfilerScope(TracyCZoneCtx context)
    {
        _context = context;
    }

    /// <summary>
    /// Disposes the profiling scope, marking the end of the associated Tracy zone.
    /// </summary>
    public void Dispose()
    {
#if REX_TRACY
        if (_context is null)
        {
            return;
        }

        TracyEmitZoneEnd(_context.Value);
#endif
    }

    /// <summary>
    /// Sets the name of this profiling scope in the Tracy profiler. This name will be displayed in the profiler UI to help identify the zone. If the name is null or empty, no name will be set and the default zone name will be used.
    /// </summary>
    /// <param name="name">The name to set for this profiling scope. If null or empty, no name will be set.</param>
    [Conditional("REX_TRACY")]
    public void ZoneName(string name)
    {
        if (string.IsNullOrEmpty(name) || _context is null)
        {
            return;
        }

        var nameStr = CString.FromString(name);
        int strLength = Encoding.UTF8.GetByteCount(name);

        TracyEmitZoneName(_context.Value, nameStr, (ulong)strLength);
    }

    /// <summary>
    /// Sets the name of this profiling scope in the Tracy profiler. This name will be displayed in the profiler UI to help identify the zone. If the name is null or empty, no name will be set and the default zone name will be used.
    /// </summary>
    /// <param name="name">The name to set for this profiling scope. If null or empty, no name will be set.</param>
    public void SetName(string name)
    {
        var nameStr = CString.FromString(name);
        TracyEmitZoneName(_context, nameStr, (ulong)name.Length);
    }

    /// <summary>
    /// Sets the text associated with this profiling scope in the Tracy profiler. This text will be displayed in the profiler UI when hovering over the zone, providing additional context about the zone's purpose or behavior.
    /// </summary>
    /// <param name="text">The text to associate with this profiling scope. If null or empty, no text will be set.</param>
    [Conditional("REX_TRACY")]
    public void ZoneText(string text)
    {
        if (string.IsNullOrEmpty(text) || _context is null)
        {
            return;
        }

        var textStr = CString.FromString(text);
        int strLength = Encoding.UTF8.GetByteCount(text);

        TracyEmitZoneText(_context.Value, textStr, (ulong)strLength);
    }

    /// <summary>
    /// Sets the color of this profiling scope in the Tracy profiler UI.
    /// </summary>
    /// <param name="color">The color to set for this profiling scope, specified as a 32-bit unsigned integer (ARGB format). If the value is 0, no color will be set and the default color will be used.</param>
    [Conditional("REX_TRACY")]
    public void ZoneColor(uint color)
    {
        if (color != 0 && _context is not null)
        {
            TracyEmitZoneColor(_context.Value, color);
        }
    }
}