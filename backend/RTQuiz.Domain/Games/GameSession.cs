namespace RTQuiz.Domain.Games;

public enum GamePhase
{
    Lobby = 0,
    InProgress = 1,
}

public sealed class GameSession
{
    private readonly List<Player> _players = new();

    public GameSession(RoomCode roomCode, DateTime createdAtUtc)
    {
        RoomCode = roomCode;
        CreatedAtUtc = createdAtUtc;
        Phase = GamePhase.Lobby;
        CurrentQuestionIndex = -1;
    }

    public RoomCode RoomCode { get; }
    public DateTime CreatedAtUtc { get; }

    public IReadOnlyList<Player> Players => _players;

    public string? HostPlayerId { get; private set; }
    public GamePhase Phase { get; private set; }
    public int CurrentQuestionIndex { get; private set; }

    public Player AddPlayer(string playerName)
    {
        playerName = (playerName ?? "").Trim();
        if (playerName.Length < 2 || playerName.Length > 20)
            throw new ArgumentException("PlayerName must be 2-20 chars.");

        var player = new Player(Guid.NewGuid().ToString("N"), playerName);
        _players.Add(player);

        if (HostPlayerId is null)
            HostPlayerId = player.Id;

        return player;
    }

    public void StartGame(string playerId)
    {
        if (HostPlayerId is null)
            throw new InvalidOperationException("Game has no host.");

        if (playerId != HostPlayerId)
            throw new InvalidOperationException("Only host can start the game.");

        if (Phase != GamePhase.Lobby)
            throw new InvalidOperationException("Game already started.");

        if (_players.Count < 1)
            throw new InvalidOperationException("Game must have at least 1 player.");

        Phase = GamePhase.InProgress;
        CurrentQuestionIndex = 0;
    }
}
