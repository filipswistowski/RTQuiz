namespace RTQuiz.Domain.Games;

public sealed class GameSession
{
    private readonly List<Player> _players = new();

    public GameSession(RoomCode roomCode, DateTime createdAtUtc)
    {
        RoomCode = roomCode;
        CreatedAtUtc = createdAtUtc;
    }

    public RoomCode RoomCode { get; }
    public DateTime CreatedAtUtc { get; }

    public IReadOnlyList<Player> Players => _players;

    public Player AddPlayer(string playerName)
    {
        playerName = (playerName ?? "").Trim();

        if (playerName.Length < 2 || playerName.Length > 20)
            throw new ArgumentException("PlayerName must be 2-20 chars.");

        var player = new Player(Guid.NewGuid().ToString("N"), playerName); // GUID v4 [web:530]
        _players.Add(player);
        return player;
    }
}
