using RTQuiz.Domain.Games;

namespace RTQuiz.Application.Games;

public interface IGameSessionStore
{
    GameSession CreateNew();
    bool Exists(RoomCode roomCode);
}
