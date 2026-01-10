using RTQuiz.Domain.Games;

namespace RTQuiz.Application.Games;

public interface IGameSessionStore
{
    GameSession CreateNew();
    bool Exists(RoomCode roomCode);

    bool TryJoin(RoomCode roomCode, string playerName, out Player player);

    bool TryGet(RoomCode roomCode, out GameSession session);

    bool TryStart(RoomCode roomCode, string playerId, out GameSession session, out string error);
}
