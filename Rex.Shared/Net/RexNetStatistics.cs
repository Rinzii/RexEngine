namespace Rex.Shared.Net;

/// <summary>Tracks network bandwidth and message counts by type.</summary>
public sealed class RexNetStatistics
{
    private long _bytesSent;
    private long _bytesReceived;
    private long _messagesSent;
    private long _messagesReceived;
    private readonly Dictionary<ushort, long> _messageCountByType = new();
    private readonly Dictionary<ushort, long> _bytesByType = new();
    private readonly Lock _lock = new();

    public long BytesSent => Interlocked.Read(ref _bytesSent);
    public long BytesReceived => Interlocked.Read(ref _bytesReceived);
    public long MessagesSent => Interlocked.Read(ref _messagesSent);
    public long MessagesReceived => Interlocked.Read(ref _messagesReceived);

    /// <summary>Adds send-side totals and per-type counts. Thread safe.</summary>
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

    /// <summary>Adds receive totals and per-type message counts. Byte totals only when you pass a real id.</summary>
    /// <param name="messageId">Type id when known. Callers may pass 0 before deserialize.</param>
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

    /// <summary>Snapshot of counts. Bytes are only filled for types that went through <see cref="RecordSent"/>.</summary>
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

    /// <summary>Clears all counters and dictionaries.</summary>
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