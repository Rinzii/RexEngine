namespace Rex.Shared.Net;

/// <summary>
/// Tracks network bandwidth and message counts, broken down by message type.
/// </summary>
public sealed class RexNetStatistics
{
    private long _bytesSent;
    private long _bytesReceived;
    private long _messagesSent;
    private long _messagesReceived;
    private readonly Dictionary<ushort, long> _messageCountByType = new();
    private readonly Dictionary<ushort, long> _bytesByType = new();
    private readonly object _lock = new();

    /// <summary>
    /// Gets the total bytes sent.
    /// </summary>
    public long BytesSent => Interlocked.Read(ref _bytesSent);

    /// <summary>
    /// Gets the total bytes received.
    /// </summary>
    public long BytesReceived => Interlocked.Read(ref _bytesReceived);

    /// <summary>
    /// Gets the total messages sent.
    /// </summary>
    public long MessagesSent => Interlocked.Read(ref _messagesSent);

    /// <summary>
    /// Gets the total messages received.
    /// </summary>
    public long MessagesReceived => Interlocked.Read(ref _messagesReceived);

    /// <summary>
    /// Records one outbound message.
    /// </summary>
    public void RecordSent(ushort messageId, int bytes)
    {
        Interlocked.Add(ref _bytesSent, bytes);
        Interlocked.Increment(ref _messagesSent);

        lock (_lock)
        {
            _messageCountByType.TryGetValue(messageId, out var count);
            _messageCountByType[messageId] = count + 1;

            _bytesByType.TryGetValue(messageId, out var totalBytes);
            _bytesByType[messageId] = totalBytes + bytes;
        }
    }

    /// <summary>
    /// Records one inbound message.
    /// </summary>
    public void RecordReceived(ushort messageId, int bytes)
    {
        Interlocked.Add(ref _bytesReceived, bytes);
        Interlocked.Increment(ref _messagesReceived);

        lock (_lock)
        {
            _messageCountByType.TryGetValue(messageId, out var count);
            _messageCountByType[messageId] = count + 1;
        }
    }

    /// <summary>
    /// Returns per-message totals for the current sample window.
    /// </summary>
    public Dictionary<ushort, (long Count, long Bytes)> GetPerTypeStats()
    {
        lock (_lock)
        {
            var result = new Dictionary<ushort, (long, long)>();
            foreach (var (msgId, count) in _messageCountByType)
            {
                _bytesByType.TryGetValue(msgId, out var bytes);
                result[msgId] = (count, bytes);
            }
            return result;
        }
    }

    /// <summary>
    /// Clears all counters.
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _bytesSent, 0);
        Interlocked.Exchange(ref _bytesReceived, 0);
        Interlocked.Exchange(ref _messagesSent, 0);
        Interlocked.Exchange(ref _messagesReceived, 0);

        lock (_lock)
        {
            _messageCountByType.Clear();
            _bytesByType.Clear();
        }
    }
}
