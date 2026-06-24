using System;
using System.Collections.Generic;
using System.Linq;

namespace RTQuiz.Api.Services;

public sealed class InMemoryPresenceStore
{
    private readonly object _lock = new();

    // connectionId -> (roomCode, playerId)
    private readonly Dictionary<string, (string RoomCode, string PlayerId)> _byConnection = new();

    // roomCode -> Set of playerIds that have at least one connection
    private readonly Dictionary<string, HashSet<string>> _onlineByRoom = new();

    public void Identify(string connectionId, string roomCode, string playerId)
    {
        lock (_lock)
        {
            // If this connection was already identified, remove old mapping first
            if (_byConnection.TryGetValue(connectionId, out var prev))
            {
                RemoveInternal(connectionId, prev.RoomCode, prev.PlayerId);
            }

            _byConnection[connectionId] = (roomCode, playerId);

            if (!_onlineByRoom.TryGetValue(roomCode, out var players))
            {
                players = new HashSet<string>();
                _onlineByRoom[roomCode] = players;
            }
            players.Add(playerId);
        }
    }

    public void RemoveByConnection(string connectionId)
    {
        lock (_lock)
        {
            if (_byConnection.Remove(connectionId, out var map))
            {
                RemoveInternal(connectionId, map.RoomCode, map.PlayerId);
            }
        }
    }

    private void RemoveInternal(string connectionId, string roomCode, string playerId)
    {
        // A player is only offline if no other connection maps to the same player
        var hasOtherConnections = false;
        foreach (var kvp in _byConnection)
        {
            if (kvp.Key != connectionId && kvp.Value.RoomCode == roomCode && kvp.Value.PlayerId == playerId)
            {
                hasOtherConnections = true;
                break;
            }
        }

        if (!hasOtherConnections)
        {
            if (_onlineByRoom.TryGetValue(roomCode, out var players))
            {
                players.Remove(playerId);
                if (players.Count == 0)
                {
                    _onlineByRoom.Remove(roomCode);
                }
            }
        }
    }

    public IReadOnlyList<string> GetOnlinePlayerIds(string roomCode)
    {
        lock (_lock)
        {
            if (!_onlineByRoom.TryGetValue(roomCode, out var players))
                return Array.Empty<string>();

            return players.OrderBy(x => x).ToList();
        }
    }

    public bool TryGetRoomForConnection(string connectionId, out string roomCode)
    {
        lock (_lock)
        {
            roomCode = "";
            if (!_byConnection.TryGetValue(connectionId, out var map))
                return false;

            roomCode = map.RoomCode;
            return true;
        }
    }
}
