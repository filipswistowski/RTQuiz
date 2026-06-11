using Microsoft.AspNetCore.SignalR;
using RTQuiz.Api.Hubs;
using RTQuiz.Application.Games;
using RTQuiz.Domain.Games;
using RTQuiz.Infrastructure.Games;

namespace RTQuiz.Api.Services;

public sealed class QuestionTimerService : BackgroundService
{
    private readonly IGameSessionStore _store;
    private readonly IQuestionBank _questionBank;
    private readonly IHubContext<GameHub> _hub;

    public QuestionTimerService(
        IGameSessionStore store,
        IQuestionBank questionBank,
        IHubContext<GameHub> hub)
    {
        _store = store;
        _questionBank = questionBank;
        _hub = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tick = TimeSpan.FromMilliseconds(250);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(tick, stoppingToken);

            // This timer only supports the in-memory store implementation.
            if (_store is not InMemoryGameSessionStore mem)
                continue;

            var now = DateTime.UtcNow;
            var questions = _questionBank.GetAll();
            if (questions.Count == 0)
                continue;

            foreach (var session in mem.GetAllSessions())
            {
                // Copy everything needed for async work outside the lock
                Question? revealedQuestion = null;
                bool gameFinished = false;
                QuestionRevealedPayload? revealPayload = null;
                List<ScoreboardEntry>? finalScores = null;

                lock (session)
                {
                    if (!session.IsQuestionTimedOut(now))
                        continue;

                    if (session.CurrentQuestionIndex < 0 || session.CurrentQuestionIndex >= questions.Count)
                        continue;

                    revealedQuestion = questions[session.CurrentQuestionIndex];

                    // Close question + score
                    session.RevealAnswerAndScore(revealedQuestion.CorrectIndex);

                    // 1) Prepare payload for Reveal & Scoreboard update while we still hold the lock
                    var totalPlayers = session.Players.Count;
                    var totalAnswered = session.CurrentAnswers.Count;

                    var counts = new int[revealedQuestion.Answers.Count];
                    foreach (var kv in session.CurrentAnswers)
                    {
                        var answerIndex = kv.Value;
                        if (answerIndex >= 0 && answerIndex < counts.Length)
                            counts[answerIndex]++;
                    }

                    var percentages = counts
                        .Select(c => totalAnswered == 0 ? 0.0 : Math.Round((double)c * 100.0 / totalAnswered, 1))
                        .ToArray();

                    var scoresPayload = session.Players
                        .Select(p => new ScoreboardEntry(
                            p.Id,
                            p.Name,
                            session.Scores.TryGetValue(p.Id, out var pts) ? pts : 0
                        ))
                        .OrderByDescending(x => x.Points)
                        .ToList();

                    revealPayload = new QuestionRevealedPayload(
                        session.RoomCode.Value,
                        totalPlayers,
                        totalAnswered,
                        counts,
                        percentages,
                        scoresPayload
                    );

                    // Auto-finish if this was the last question
                    if (session.CurrentQuestionIndex >= questions.Count - 1 && session.Phase != GamePhase.Finished)
                    {
                        session.FinishGame();
                        gameFinished = true;

                        finalScores = session.Players
                            .Select(p => new ScoreboardEntry(
                                p.Id,
                                p.Name,
                                session.Scores.TryGetValue(p.Id, out var pts) ? pts : 0
                            ))
                            .OrderByDescending(x => x.Points)
                            .ToList();
                    }
                }

                // Fire events outside lock (don't block other requests) using pre-built snapshots!
                if (revealPayload is not null && revealedQuestion is not null)
                {
                    _ = FireRevealAndScoreboardAsync(revealPayload, revealedQuestion, stoppingToken);

                    if (gameFinished && finalScores is not null)
                        _ = FireGameFinishedAsync(revealPayload.RoomCode, finalScores, stoppingToken);
                }
            }
        }
    }

    private async Task FireRevealAndScoreboardAsync(QuestionRevealedPayload payload, Question q, CancellationToken ct)
    {
        var room = payload.RoomCode;

        await _hub.Clients
            .Group($"room:{room}")
            .SendAsync("AnswerRevealed", new { questionId = q.Id, correctIndex = q.CorrectIndex }, ct);

        await _hub.Clients
            .Group($"room:{room}")
            .SendAsync("AnswerStatsRevealed", new
            {
                roomCode = room,
                questionId = q.Id,
                totalPlayers = payload.TotalPlayers,
                totalAnswered = payload.TotalAnswered,
                counts = payload.Counts,
                percentages = payload.Percentages
            }, ct);

        var scoreboardPayload = payload.Scores
            .Select(s => new
            {
                playerId = s.PlayerId,
                name = s.Name,
                points = s.Points
            })
            .ToList();

        await _hub.Clients
            .Group($"room:{room}")
            .SendAsync("ScoreboardUpdated", new { roomCode = room, scores = scoreboardPayload }, ct);
    }

    private async Task FireGameFinishedAsync(string roomCode, List<ScoreboardEntry> finalScores, CancellationToken ct)
    {
        var scoresPayload = finalScores
            .Select(s => new
            {
                playerId = s.PlayerId,
                name = s.Name,
                points = s.Points
            })
            .ToList();

        await _hub.Clients
            .Group($"room:{roomCode}")
            .SendAsync("GameFinished", new { roomCode = roomCode, scores = scoresPayload }, ct);
    }

    private sealed record ScoreboardEntry(string PlayerId, string Name, int Points);
    private sealed record QuestionRevealedPayload(
        string RoomCode,
        int TotalPlayers,
        int TotalAnswered,
        int[] Counts,
        double[] Percentages,
        List<ScoreboardEntry> Scores
    );
}
