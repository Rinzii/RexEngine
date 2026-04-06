using bottlenoselabs.C2CS.Runtime;

namespace Rex.Shared.Profiling.Tracy;

using static global::Tracy.PInvoke;

public readonly struct TracyProfilerScope : IDisposable
{
    public readonly TracyCZoneCtx Context;

    public TracyProfilerScope(TracyCZoneCtx context)
    {
        Context = context;
    }

    public void Dispose()
    {
        TracyEmitZoneEnd(Context);
    }

    public void SetText(string text)
    {
        var textStr = CString.FromString(text);
        TracyEmitZoneText(Context, textStr, (ulong)text.Length);
    }

    public void SetColor(uint color)
    {
        if (color != 0)
        {
            TracyEmitZoneColor(Context, color);
        }
    }
}
