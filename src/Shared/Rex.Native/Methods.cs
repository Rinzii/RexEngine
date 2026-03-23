namespace Rex.Native;

public static partial class Methods
{
    public static TickResult GetTickResult(int current)
    {
        var next = Tick(current);
        return new TickResult(next, next * 2);
    }
}
