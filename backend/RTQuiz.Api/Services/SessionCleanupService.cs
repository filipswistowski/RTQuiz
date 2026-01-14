using RTQuiz.Domain.Games;
using RTQuiz.Infrastructure.Games;

namespace RTQuiz.Api.Services;

public sealed class SessionCleanupService : BackgroundService
{
    private static readonly TimeSpan LobbyTtl = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan InProgressTtl = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);

    private readonly InMemoryGameSessionStore _store;

    public SessionCleanupService(InMemoryGameSessionStore store)
    {
        _store = store;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Tick, stoppingToken);

            var now = DateTime.UtcNow;

            foreach (var session in _store.GetAllSessions())
            {
                var ttl = session.Phase == GamePhase.Lobby ? LobbyTtl : InProgressTtl;
                var expired = now - session.LastActivityUtc > ttl;

                if (!expired) continue;

                _store.TryRemove(session.RoomCode);
            }
        }
    }
}
