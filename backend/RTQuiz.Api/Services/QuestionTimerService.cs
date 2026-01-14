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

                lock (session)
                {
                    if (!session.IsQuestionTimedOut(now))
                        continue;

                    if (session.CurrentQuestionIndex < 0 || session.CurrentQuestionIndex >= questions.Count)
                        continue;

                    revealedQuestion = questions[session.CurrentQuestionIndex];

                    // Close question + score
                    session.RevealAnswerAndScore(revealedQuestion.CorrectIndex);

                    // Auto-finish if this was the last question
                    if (session.CurrentQuestionIndex >= questions.Count - 1 && session.Phase != GamePhase.Finished)
                    {
                        session.FinishGame();
                        gameFinished = true;
                    }

                }

                // Fire events outside lock (don't block other requests)
                if (revealedQuestion is not null)
                {
                    _ = FireRevealAndScoreboardAsync(session, revealedQuestion, stoppingToken);

                    if (gameFinished)
                        _ = FireGameFinishedAsync(session, stoppingToken);
                }
            }
        }
    }

    private async Task FireRevealAndScoreboardAsync(GameSession session, Question q, CancellationToken ct)
    {
        var room = session.RoomCode.Value;

        await _hub.Clients
            .Group($"room:{room}")
            .SendAsync("AnswerRevealed", new { questionId = q.Id, correctIndex = q.CorrectIndex }, ct);

        var scoresPayload = session.Players
            .Select(p => new
            {
                playerId = p.Id,
                name = p.Name,
                points = session.Scores.TryGetValue(p.Id, out var pts) ? pts : 0
            })
            .OrderByDescending(x => x.points)
            .ToList();

        await _hub.Clients
            .Group($"room:{room}")
            .SendAsync("ScoreboardUpdated", new { roomCode = room, scores = scoresPayload }, ct);
    }

    private async Task FireGameFinishedAsync(GameSession session, CancellationToken ct)
    {
        var room = session.RoomCode.Value;

        var finalScores = session.Players
            .Select(p => new
            {
                playerId = p.Id,
                name = p.Name,
                points = session.Scores.TryGetValue(p.Id, out var pts) ? pts : 0
            })
            .OrderByDescending(x => x.points)
            .ToList();

        await _hub.Clients
            .Group($"room:{room}")
            .SendAsync("GameFinished", new { roomCode = room, scores = finalScores }, ct);
    }
}
