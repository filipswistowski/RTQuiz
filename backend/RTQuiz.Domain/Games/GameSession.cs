namespace RTQuiz.Domain.Games;

public enum GamePhase
{
    Lobby = 0,
    InProgress = 1,
    Finished = 2
}

public sealed class GameSession
{
    private readonly List<Player> _players = new();
    public DateTime? QuestionOpenedAtUtc { get; private set; }
    public int QuestionDurationSeconds { get; private set; } = 15; // na start stałe 15s
    public DateTime LastActivityUtc { get; private set; } = DateTime.UtcNow;
    public void Touch() => LastActivityUtc = DateTime.UtcNow;

    // per-question state
    private readonly Dictionary<string, int> _currentAnswers = new(); // playerId -> answerIndex

    // whole game state
    private readonly Dictionary<string, int> _scores = new(); // playerId -> points

    public GameSession(RoomCode roomCode, DateTime createdAtUtc)
    {
        RoomCode = roomCode;
        CreatedAtUtc = createdAtUtc;

        Phase = GamePhase.Lobby;
        CurrentQuestionIndex = -1;

        IsQuestionOpen = false;
    }

    public RoomCode RoomCode { get; }
    public DateTime CreatedAtUtc { get; }

    public IReadOnlyList<Player> Players => _players;

    public string? HostPlayerId { get; private set; }
    public GamePhase Phase { get; private set; }
    public int CurrentQuestionIndex { get; private set; }

    public bool IsQuestionOpen { get; private set; }

    public IReadOnlyDictionary<string, int> Scores => _scores;

    public Player AddPlayer(string playerName)
    {
        playerName = (playerName ?? "").Trim();
        if (playerName.Length < 2 || playerName.Length > 20)
            throw new ArgumentException("PlayerName must be 2-20 chars.");

        var player = new Player(Guid.NewGuid().ToString("N"), playerName);
        _players.Add(player);

        if (HostPlayerId is null)
            HostPlayerId = player.Id;

        if (!_scores.ContainsKey(player.Id))
            _scores[player.Id] = 0;

        Touch();
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

        _currentAnswers.Clear();
        IsQuestionOpen = true;
        QuestionOpenedAtUtc = DateTime.UtcNow;
        Touch();
    }

    public void SubmitAnswer(string playerId, int answerIndex)
    {
        if (Phase != GamePhase.InProgress)
            throw new InvalidOperationException("Game not in progress.");

        if (!IsQuestionOpen)
            throw new InvalidOperationException("Question is closed.");

        if (_players.All(p => p.Id != playerId))
            throw new InvalidOperationException("Unknown player.");

        if (answerIndex < 0)
            throw new InvalidOperationException("Invalid answerIndex.");

        // overwrite allowed
        _currentAnswers[playerId] = answerIndex;

        // ensure score slot exists
        if (!_scores.ContainsKey(playerId))
            _scores[playerId] = 0;

        Touch();
    }

    public void RevealAnswerAndScore(int correctIndex)
    {
        if (Phase != GamePhase.InProgress)
            throw new InvalidOperationException("Game not in progress.");

        if (!IsQuestionOpen)
            throw new InvalidOperationException("Question already closed.");

        IsQuestionOpen = false;
        QuestionOpenedAtUtc = null;

        foreach (var (pid, ans) in _currentAnswers)
        {
            if (ans == correctIndex)
                _scores[pid] = (_scores.TryGetValue(pid, out var pts) ? pts : 0) + 1;
        }

        Touch();
    }

    public void NextQuestion(string playerId, int totalQuestions)
    {
        if (HostPlayerId is null)
            throw new InvalidOperationException("Game has no host.");

        if (playerId != HostPlayerId)
            throw new InvalidOperationException("Only host can go next.");

        if (Phase != GamePhase.InProgress)
            throw new InvalidOperationException("Game not in progress.");

        if (IsQuestionOpen)
            throw new InvalidOperationException("Close current question first.");

        var nextIndex = CurrentQuestionIndex + 1;
        if (nextIndex >= totalQuestions)
        {
            FinishGame();
            return;
        }

        CurrentQuestionIndex = nextIndex;
        _currentAnswers.Clear();
        IsQuestionOpen = true;
        QuestionOpenedAtUtc = DateTime.UtcNow;
        Touch();
    }

    public bool IsQuestionTimedOut(DateTime nowUtc)
    {
        if (!IsQuestionOpen) return false;
        if (QuestionOpenedAtUtc is null) return false;
        return nowUtc - QuestionOpenedAtUtc.Value >= TimeSpan.FromSeconds(QuestionDurationSeconds);
    }

    public void FinishGame()
    {
        if (Phase == GamePhase.Finished)
            return;

        if (Phase != GamePhase.InProgress)
            throw new InvalidOperationException("Game not in progress.");

        Phase = GamePhase.Finished;
        IsQuestionOpen = false;
        QuestionOpenedAtUtc = null;
        Touch();
    }
}
