namespace RTQuiz.Domain.Games;

public sealed class GameSession
{
    public GameSession(RoomCode roomCode, DateTime createdAtUtc)
    {
        RoomCode = roomCode;
        CreatedAtUtc = createdAtUtc;
    }

    public RoomCode RoomCode { get; }
    public DateTime CreatedAtUtc { get; }
}
