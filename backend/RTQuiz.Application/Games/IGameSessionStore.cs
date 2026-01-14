using RTQuiz.Domain.Games;

namespace RTQuiz.Application.Games;

public interface IGameSessionStore
{
    GameSession CreateNew();
    bool Exists(RoomCode roomCode);
    bool TryJoin(RoomCode roomCode, string playerName, out Player player);
    bool TryGet(RoomCode roomCode, out GameSession session);
    bool TryStart(RoomCode roomCode, string playerId, out GameSession session, out string error);
    bool TrySubmitAnswer(RoomCode roomCode, string playerId, int answerIndex, int answersCount, out GameSession session, out string error);
    bool TryReveal(RoomCode roomCode, string playerId, int correctIndex, out GameSession session, out string error);
    bool TryNext(RoomCode roomCode, string playerId, int totalQuestions, out GameSession session, out string error);
}
