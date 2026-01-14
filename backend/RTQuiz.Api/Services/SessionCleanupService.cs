using Microsoft.Extensions.Options;
using RTQuiz.Api.Options;
using RTQuiz.Domain.Games;
using RTQuiz.Infrastructure.Games;

namespace RTQuiz.Api.Services;

public sealed class SessionCleanupService : BackgroundService
{
    private readonly InMemoryGameSessionStore _store;
    private readonly SessionCleanupOptions _opt;

    public SessionCleanupService(InMemoryGameSessionStore store, IOptions<SessionCleanupOptions> opt)
    {
        _store = store;
        _opt = opt.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tick = TimeSpan.FromSeconds(_opt.TickSeconds);
        var lobbyTtl = TimeSpan.FromMinutes(_opt.LobbyTtlMinutes);
        var inProgressTtl = TimeSpan.FromMinutes(_opt.InProgressTtlMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(tick, stoppingToken);

            var now = DateTime.UtcNow;

            foreach (var session in _store.GetAllSessions())
            {
                var ttl = session.Phase == GamePhase.Lobby ? lobbyTtl : inProgressTtl;
                var expired = now - session.LastActivityUtc > ttl;

                if (!expired) continue;

                _store.TryRemove(session.RoomCode);
            }
        }
    }
}
