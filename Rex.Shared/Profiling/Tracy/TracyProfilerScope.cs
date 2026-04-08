using System.Text;
using bottlenoselabs.C2CS.Runtime;

namespace Rex.Shared.Profiling.Tracy;

using static global::Tracy.PInvoke;

/// <summary>
/// Represents a profiling scope for the Tracy profiler.
/// </summary>
public readonly struct TracyProfilerScope : IDisposable
{
    private readonly TracyCZoneCtx _context;

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
        TracyEmitZoneEnd(_context);
    }

    /// <summary>
    /// Sets the name of this profiling scope in the Tracy profiler. This name will be displayed in the profiler UI to help identify the zone. If the name is null or empty, no name will be set and the default zone name will be used.
    /// </summary>
    /// <param name="name">The name to set for this profiling scope. If null or empty, no name will be set.</param>
    public void SetName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        var nameStr = CString.FromString(name);
        var strLength = Encoding.UTF8.GetByteCount(name);

        TracyEmitZoneName(_context, nameStr, (ulong)strLength);
    }

    /// <summary>
    /// Sets the text associated with this profiling scope in the Tracy profiler. This text will be displayed in the profiler UI when hovering over the zone, providing additional context about the zone's purpose or behavior.
    /// </summary>
    /// <param name="text">The text to associate with this profiling scope. If null or empty, no text will be set.</param>
    public void SetText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var textStr = CString.FromString(text);
        var strLength = Encoding.UTF8.GetByteCount(text);

        TracyEmitZoneText(_context, textStr, (ulong)strLength);
    }

    /// <summary>
    /// Sets the color of this profiling scope in the Tracy profiler UI.
    /// </summary>
    /// <param name="color">The color to set for this profiling scope, specified as a 32-bit unsigned integer in RGB format (0xRRGGBB). If the value is 0, no color will be set and the default color will be used.</param>
    public void SetColor(uint color)
    {
        if (color != 0)
        {
            TracyEmitZoneColor(_context, color);
        }
    }
}