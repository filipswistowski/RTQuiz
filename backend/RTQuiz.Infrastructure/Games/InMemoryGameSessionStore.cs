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

    public bool TryGet(RoomCode roomCode, out GameSession session)
    => _sessions.TryGetValue(roomCode.Value, out session!);

    public bool TryStart(RoomCode roomCode, string playerId, out GameSession session, out string error)
    {
        error = "";
        session = default!;

        if (!_sessions.TryGetValue(roomCode.Value, out session))
        {
            error = "NotFound";
            return false;
        }

        lock (session)
        {
            try
            {
                session.StartGame(playerId);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    public bool TrySubmitAnswer(RoomCode roomCode, string playerId, int answerIndex, out GameSession session, out string error)
    {
        error = "";
        session = default!;

        if (!_sessions.TryGetValue(roomCode.Value, out session))
        {
            error = "NotFound";
            return false;
        }

        lock (session)
        {
            try
            {
                session.SubmitAnswer(playerId, answerIndex);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    public bool TryReveal(RoomCode roomCode, string playerId, int correctIndex, out GameSession session, out string error)
    {
        error = "";
        session = default!;

        if (!_sessions.TryGetValue(roomCode.Value, out session))
        {
            error = "NotFound";
            return false;
        }

        lock (session)
        {
            try
            {
                // host-only enforced in GameSession.NextQuestion; here do it too:
                if (session.HostPlayerId is null)
                    throw new InvalidOperationException("Game has no host.");
                if (playerId != session.HostPlayerId)
                    throw new InvalidOperationException("Only host can reveal the answer.");

                session.RevealAnswerAndScore(correctIndex);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    public bool TryNext(RoomCode roomCode, string playerId, int totalQuestions, out GameSession session, out string error)
    {
        error = "";
        session = default!;

        if (!_sessions.TryGetValue(roomCode.Value, out session))
        {
            error = "NotFound";
            return false;
        }

        lock (session)
        {
            try
            {
                session.NextQuestion(playerId, totalQuestions);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    public IEnumerable<GameSession> GetAllSessions() => _sessions.Values;
}
