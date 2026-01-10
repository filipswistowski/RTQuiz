using System.Collections.Concurrent;
using RTQuiz.Application.Games;
using RTQuiz.Domain.Games;

namespace RTQuiz.Infrastructure.Games;

public sealed class InMemoryGameSessionStore : IGameSessionStore
{
    private readonly ConcurrentDictionary<string, GameSession> _sessions = new();
    private readonly IRoomCodeGenerator _roomCodeGenerator;

    public InMemoryGameSessionStore(IRoomCodeGenerator roomCodeGenerator)
    {
        _roomCodeGenerator = roomCodeGenerator;
    }

    public GameSession CreateNew()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = _roomCodeGenerator.Generate();
            var session = new GameSession(code, DateTime.UtcNow);

            if (_sessions.TryAdd(code.Value, session))
                return session;
        }

        throw new InvalidOperationException("Failed to allocate unique room code.");
    }

    public bool Exists(RoomCode roomCode) => _sessions.ContainsKey(roomCode.Value);

    public bool TryJoin(RoomCode roomCode, string playerName, out Player player)
    {
        player = default!;

        if (!_sessions.TryGetValue(roomCode.Value, out var session))
            return false;

        lock (session)
        {
            player = session.AddPlayer(playerName);
            return true;
        }
    }
}
