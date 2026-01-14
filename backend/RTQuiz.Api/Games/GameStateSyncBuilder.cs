using RTQuiz.Api.Contracts;
using RTQuiz.Api.Services;
using RTQuiz.Application.Games;
using RTQuiz.Domain.Games;

namespace RTQuiz.Api.Games;

public static class GameStateSyncBuilder
{
    public static GameStateSync Build(GameSession session, IQuestionBank questionBank, InMemoryPresenceStore presence)
    {
        // NOTE (latency compensation):
        // We send the server timestamp along with "questionEndsInMs" so the client can estimate
        // the remaining time more accurately by factoring in network latency.
        // Example on the client:
        //   latencyMs ~= (clientReceivedUtcMs - ServerNowUtcMs)
        //   adjustedRemainingMs = max(0, QuestionEndsInMs - latencyMs)
        var serverNowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        long? questionEndsInMs = null;

        if (session.IsQuestionOpen && session.QuestionOpenedAtUtc is not null)
        {
            var endUtc = session.QuestionOpenedAtUtc.Value.AddSeconds(session.QuestionDurationSeconds);
            var ms = (long)Math.Ceiling((endUtc - DateTime.UtcNow).TotalMilliseconds);
            if (ms < 0) ms = 0;
            questionEndsInMs = ms;
        }

        var players = session.Players
            .Select(p => new GameStatePlayerDto(p.Id, p.Name))
            .ToList();

        GameStateQuestionDto? currentQuestion = null;

        if (session.Phase == GamePhase.InProgress && session.IsQuestionOpen)
        {
            var questions = questionBank.GetAll();
            if (session.CurrentQuestionIndex >= 0 && session.CurrentQuestionIndex < questions.Count)
            {
                var q = questions[session.CurrentQuestionIndex];
                currentQuestion = new GameStateQuestionDto(q.Id, q.Text, q.Answers);
            }
        }

        // scoreboard: pokazujemy wszystkich graczy (nawet jak ktoś nie odpowiada / jest offline)
        var scores = session.Players
            .Select(p => new GameStateScoreDto(
                p.Id,
                p.Name,
                session.Scores.TryGetValue(p.Id, out var pts) ? pts : 0
            ))
            .OrderByDescending(s => s.Points)
            .ToList();

        var onlinePlayerIds = presence.GetOnlinePlayerIds(session.RoomCode.Value).ToList();

        return new GameStateSync(
            session.RoomCode.Value,
            session.Phase.ToString(),
            session.IsQuestionOpen,
            questionEndsInMs,
            serverNowUtcMs,
            onlinePlayerIds,
            players,
            currentQuestion,
            scores
        );
    }
}
