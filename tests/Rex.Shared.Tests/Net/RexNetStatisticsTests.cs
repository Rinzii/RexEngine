using Rex.Shared.Net;

namespace Rex.Shared.Tests.Net;

// Send and receive counters and per-type snapshots.
public sealed class RexNetStatisticsTests
{
    [Fact]
    // Send side totals and byte sums per message id.
    public void RecordSent_accumulates_totals_and_per_type_bytes()
    {
        var stats = new RexNetStatistics();
        stats.RecordSent(10, 100);
        stats.RecordSent(10, 50);
        stats.RecordSent(20, 25);

        Assert.Equal(175, stats.BytesSent);
        Assert.Equal(3, stats.MessagesSent);
        Assert.Equal(0, stats.BytesReceived);
        Assert.Equal(0, stats.MessagesReceived);

        var perType = stats.GetPerTypeStats();
        Assert.Equal(2, perType.Count);
        Assert.Equal((2L, 150L), perType[10]);
        Assert.Equal((1L, 25L), perType[20]);
    }

    [Fact]
    // Receive side counts per id but leaves byte totals zero without RecordSent.
    public void RecordReceived_increments_counts_and_per_type_without_send_side_bytes()
    {
        var stats = new RexNetStatistics();
        stats.RecordReceived(0, 500);
        stats.RecordReceived(7, 100);

        Assert.Equal(600, stats.BytesReceived);
        Assert.Equal(2, stats.MessagesReceived);
        Assert.Equal(0, stats.BytesSent);

        var perType = stats.GetPerTypeStats();
        Assert.Equal(2, perType.Count);
        Assert.Equal((1L, 0L), perType[0]);
        Assert.Equal((1L, 0L), perType[7]);
    }

    [Fact]
    // Reset drops all totals and per-type maps.
    public void Reset_clears_everything()
    {
        var stats = new RexNetStatistics();
        stats.RecordSent(1, 10);
        stats.RecordReceived(1, 20);
        stats.Reset();

        Assert.Equal(0, stats.BytesSent);
        Assert.Equal(0, stats.BytesReceived);
        Assert.Equal(0, stats.MessagesSent);
        Assert.Equal(0, stats.MessagesReceived);
        Assert.Empty(stats.GetPerTypeStats());
    }
}
