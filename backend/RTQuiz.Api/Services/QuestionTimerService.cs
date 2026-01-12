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
        // tick co 250ms wystarczy na dev; można podbić do 1s
        var tick = TimeSpan.FromMilliseconds(250);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(tick, stoppingToken);

            // potrzebujemy enumeracji sesji -> korzystamy z implementacji in-memory
            if (_store is not InMemoryGameSessionStore mem)
                continue;

            var now = DateTime.UtcNow;
            var questions = _questionBank.GetAll();

            foreach (var session in mem.GetAllSessions())
            {
                // unikamy race: blokujemy per session jak już robisz w store
                lock (session)
                {
                    if (!session.IsQuestionTimedOut(now))
                        continue;

                    if (session.CurrentQuestionIndex < 0 || session.CurrentQuestionIndex >= questions.Count)
                        continue;

                    var q = questions[session.CurrentQuestionIndex];

                    // to zamknie pytanie i naliczy punkty
                    session.RevealAnswerAndScore(q.CorrectIndex);

                    // eventy wyślemy już poza lockiem (żeby nie trzymać locka na await)
                    _ = FireEventsAsync(session, q, stoppingToken);
                }
            }
        }
    }

    private async Task FireEventsAsync(GameSession session, Question q, CancellationToken ct)
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
            .ToList();

        await _hub.Clients
            .Group($"room:{room}")
            .SendAsync("ScoreboardUpdated", new { roomCode = room, scores = scoresPayload }, ct);
    }
}
