namespace RTQuiz.Domain.Games;

public sealed record Question(
    string Id,
    string Text,
    List<string> Answers,
    int CorrectIndex
);
