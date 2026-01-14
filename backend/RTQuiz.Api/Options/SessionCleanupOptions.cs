namespace RTQuiz.Api.Options;

public sealed class SessionCleanupOptions
{
    public const string SectionName = "SessionCleanup";

    public int TickSeconds { get; init; } = 60;
    public int LobbyTtlMinutes { get; init; } = 20;
    public int InProgressTtlMinutes { get; init; } = 60;
}