using LiteNetLib;

namespace Rex.Shared.Net;

/// <summary>
/// Applies explicit engine defaults to LiteNetLib transports so client and server share the same runtime behavior.
/// </summary>
public static class LiteNetLibTransportConfiguration
{
    /// <summary>
    /// Applies the shared client defaults to one LiteNetLib manager instance.
    /// </summary>
    /// <param name="manager">Manager instance to configure.</param>
    public static void ApplyClientDefaults(NetManager manager)
    {
        ApplyDefaults(manager);
    }

    /// <summary>
    /// Applies the shared server defaults to one LiteNetLib manager instance.
    /// </summary>
    /// <param name="manager">Manager instance to configure.</param>
    public static void ApplyServerDefaults(NetManager manager)
    {
        ApplyDefaults(manager);
    }

    private static void ApplyDefaults(NetManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        manager.UpdateTime = ProtocolConstants.TransportUpdateTimeMs;
        manager.PingInterval = ProtocolConstants.TransportPingIntervalMs;
        manager.DisconnectTimeout = ProtocolConstants.TransportDisconnectTimeoutMs;
        manager.ReconnectDelay = ProtocolConstants.TransportReconnectDelayMs;
        manager.MaxConnectAttempts = ProtocolConstants.TransportMaxConnectAttempts;
        manager.UnsyncedEvents = false;
        manager.UnsyncedReceiveEvent = false;
        manager.UnsyncedDeliveryEvent = false;
        manager.NatPunchEnabled = false;
        manager.AutoRecycle = true;
        manager.EnableStatistics = true;
    }
}
