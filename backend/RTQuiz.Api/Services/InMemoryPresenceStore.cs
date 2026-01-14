using System.Collections.Concurrent;

namespace RTQuiz.Api.Services;

public sealed class InMemoryPresenceStore
{
    // roomCode -> set(playerId)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _onlineByRoom = new();

    // connectionId -> (roomCode, playerId)
    private readonly ConcurrentDictionary<string, (string RoomCode, string PlayerId)> _byConnection = new();

    public void Identify(string connectionId, string roomCode, string playerId)
    {
        // if this connection was already identified, remove old mapping first
        if (_byConnection.TryGetValue(connectionId, out var prev))
        {
            Remove(connectionId, prev.RoomCode, prev.PlayerId);
        }

        _byConnection[connectionId] = (roomCode, playerId);

        var set = _onlineByRoom.GetOrAdd(roomCode, _ => new ConcurrentDictionary<string, byte>());
        set[playerId] = 0;
    }

    public void RemoveByConnection(string connectionId)
    {
        if (_byConnection.TryRemove(connectionId, out var map))
        {
            Remove(connectionId, map.RoomCode, map.PlayerId);
        }
    }

    private void Remove(string connectionId, string roomCode, string playerId)
    {
        if (_onlineByRoom.TryGetValue(roomCode, out var set))
        {
            set.TryRemove(playerId, out _);

            if (set.IsEmpty)
            {
                _onlineByRoom.TryRemove(roomCode, out _);
            }
        }
    }

    public IReadOnlyList<string> GetOnlinePlayerIds(string roomCode)
    {
        if (!_onlineByRoom.TryGetValue(roomCode, out var set))
            return Array.Empty<string>();

        return set.Keys.OrderBy(x => x).ToList();
    }

    public bool TryGetRoomForConnection(string connectionId, out string roomCode)
    {
        roomCode = "";
        if (!_byConnection.TryGetValue(connectionId, out var map))
            return false;

        roomCode = map.RoomCode;
        return true;
    }
}
