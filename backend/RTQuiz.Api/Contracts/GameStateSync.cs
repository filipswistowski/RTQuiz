namespace RTQuiz.Api.Contracts;

public sealed record GameStateSync(
    string RoomCode,
    string Phase,
    bool IsQuestionOpen,
    long? QuestionEndsInMs,
    long ServerNowUtcMs,
    List<string> OnlinePlayerIds,
    List<GameStatePlayerDto> Players,
    GameStateQuestionDto? CurrentQuestion,
    List<GameStateScoreDto> Scores
);

public sealed record GameStatePlayerDto(string Id, string Name);

public sealed record GameStateQuestionDto(string Id, string Text, List<string> Answers);

public sealed record GameStateScoreDto(string PlayerId, string Name, int Points);
