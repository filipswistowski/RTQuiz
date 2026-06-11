using Xunit;
using RTQuiz.Api.Services;

namespace RTQuiz.Tests;

public class InMemoryPresenceStoreTests
{
    [Fact]
    public void Identify_ShouldMarkPlayerAsOnline()
    {
        var store = new InMemoryPresenceStore();

        store.Identify("conn1", "ROOM1", "playerA");

        var online = store.GetOnlinePlayerIds("ROOM1");
        Assert.Single(online);
        Assert.Contains("playerA", online);
    }

    [Fact]
    public void RemoveByConnection_ShouldMarkPlayerAsOffline_WhenNoOtherConnectionsExist()
    {
        var store = new InMemoryPresenceStore();

        store.Identify("conn1", "ROOM1", "playerA");
        store.RemoveByConnection("conn1");

        var online = store.GetOnlinePlayerIds("ROOM1");
        Assert.Empty(online);
    }

    [Fact]
    public void Identify_MultipleConnectionsForSamePlayer_ShouldKeepPlayerOnlineUntilAllDisconnect()
    {
        var store = new InMemoryPresenceStore();

        // Player A connects with two different connection IDs (e.g. two browser tabs)
        store.Identify("conn1", "ROOM1", "playerA");
        store.Identify("conn2", "ROOM1", "playerA");

        var onlineAfterBothConnect = store.GetOnlinePlayerIds("ROOM1");
        Assert.Single(onlineAfterBothConnect);
        Assert.Contains("playerA", onlineAfterBothConnect);

        // One tab disconnects, player should still be online
        store.RemoveByConnection("conn1");
        var onlineAfterOneDisconnect = store.GetOnlinePlayerIds("ROOM1");
        Assert.Single(onlineAfterOneDisconnect);
        Assert.Contains("playerA", onlineAfterOneDisconnect);

        // Second tab disconnects, player should now be offline
        store.RemoveByConnection("conn2");
        var onlineAfterAllDisconnect = store.GetOnlinePlayerIds("ROOM1");
        Assert.Empty(onlineAfterAllDisconnect);
    }

    [Fact]
    public void TryGetRoomForConnection_ShouldReturnCorrectRoom()
    {
        var store = new InMemoryPresenceStore();

        store.Identify("conn1", "ROOM1", "playerA");

        var found = store.TryGetRoomForConnection("conn1", out var roomCode);
        Assert.True(found);
        Assert.Equal("ROOM1", roomCode);
    }
}
