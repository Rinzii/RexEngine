using LiteNetLib;
using Rex.Shared.Net;

namespace Rex.Shared.Tests.Net;

public sealed class LiteNetLibTransportConfigurationTests
{
    [Fact]
    public void ApplyClientDefaults_sets_explicit_engine_transport_values()
    {
        EventBasedNetListener listener = new();
        NetManager manager = new(listener);

        LiteNetLibTransportConfiguration.ApplyClientDefaults(manager);

        Assert.Equal(ProtocolConstants.TransportUpdateTimeMs, manager.UpdateTime);
        Assert.Equal(ProtocolConstants.TransportPingIntervalMs, manager.PingInterval);
        Assert.Equal(ProtocolConstants.TransportDisconnectTimeoutMs, manager.DisconnectTimeout);
        Assert.Equal(ProtocolConstants.TransportReconnectDelayMs, manager.ReconnectDelay);
        Assert.Equal(ProtocolConstants.TransportMaxConnectAttempts, manager.MaxConnectAttempts);
        Assert.False(manager.UnsyncedEvents);
        Assert.False(manager.UnsyncedReceiveEvent);
        Assert.False(manager.UnsyncedDeliveryEvent);
        Assert.False(manager.NatPunchEnabled);
        Assert.True(manager.AutoRecycle);
        Assert.True(manager.EnableStatistics);
    }

    [Fact]
    public void ApplyServerDefaults_sets_explicit_engine_transport_values()
    {
        EventBasedNetListener listener = new();
        NetManager manager = new(listener);

        LiteNetLibTransportConfiguration.ApplyServerDefaults(manager);

        Assert.Equal(ProtocolConstants.TransportUpdateTimeMs, manager.UpdateTime);
        Assert.Equal(ProtocolConstants.TransportPingIntervalMs, manager.PingInterval);
        Assert.Equal(ProtocolConstants.TransportDisconnectTimeoutMs, manager.DisconnectTimeout);
        Assert.Equal(ProtocolConstants.TransportReconnectDelayMs, manager.ReconnectDelay);
        Assert.Equal(ProtocolConstants.TransportMaxConnectAttempts, manager.MaxConnectAttempts);
        Assert.False(manager.UnsyncedEvents);
        Assert.False(manager.NatPunchEnabled);
        Assert.True(manager.AutoRecycle);
        Assert.True(manager.EnableStatistics);
    }
}
