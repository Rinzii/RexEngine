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

    /// <summary>Total payload bytes sent.</summary>
    public long BytesSent => Interlocked.Read(ref _bytesSent);

    /// <summary>Total payload bytes received.</summary>
    public long BytesReceived => Interlocked.Read(ref _bytesReceived);

    /// <summary>Outbound message count.</summary>
    public long MessagesSent => Interlocked.Read(ref _messagesSent);

    /// <summary>Inbound message count.</summary>
    public long MessagesReceived => Interlocked.Read(ref _messagesReceived);

    /// <summary>Adds outbound byte and message totals plus per-type message counts.</summary>
    /// <remarks>Interlocked fields update aggregates. A lock guards per-type dictionary updates.</remarks>
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

    /// <summary>Adds inbound totals and counts for each message type. Byte totals apply only when messageId is non-zero.</summary>
    /// <param name="messageId">Type id when known. Callers may pass 0 before deserialize.</param>
    /// <param name="bytes">Payload length for this receive when known.</param>
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
