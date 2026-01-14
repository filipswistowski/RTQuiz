using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RTQuiz.Api.Games;
using RTQuiz.Api.Services;
using RTQuiz.Application.Games;
using RTQuiz.Domain.Games;

namespace RTQuiz.Api.Hubs;

public sealed class GameHub : Hub
{
    public async Task JoinRoom(
        string roomCode,
        [FromServices] IGameSessionStore store,
        [FromServices] IQuestionBank questionBank,
        [FromServices] InMemoryPresenceStore presence)
    {
        var code = RoomCode.From(roomCode);
        var groupName = $"room:{code.Value}";

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("JoinedRoom", code.Value);

        if (!store.TryGet(code, out var session))
            return;

        var snapshot = GameStateSyncBuilder.Build(session, questionBank, presence);

        // StateSync is a "snapshot" event meant for (re)initializing the client's state after reconnect/refresh.
        // It includes ServerNowUtcMs + QuestionEndsInMs so the frontend can compensate for network latency and
        // display a more accurate countdown timer.
        // Additionally, it includes OnlinePlayerIds so the UI immediately knows who's currently connected.
        await Clients.Caller.SendAsync("StateSync", snapshot);
    }

    public async Task Identify(
        string roomCode,
        string playerId,
        [FromServices] InMemoryPresenceStore presence)
    {
        var code = RoomCode.From(roomCode);

        presence.Identify(Context.ConnectionId, code.Value, playerId);

        var online = presence.GetOnlinePlayerIds(code.Value);

        await Clients.Group($"room:{code.Value}")
            .SendAsync("PresenceUpdated", new
            {
                roomCode = code.Value,
                onlinePlayerIds = online
            });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var presence = Context.GetHttpContext()?.RequestServices.GetService<InMemoryPresenceStore>();

        if (presence is not null)
        {
            // If we know which room this connection belonged to, broadcast an updated list to that room.
            if (presence.TryGetRoomForConnection(Context.ConnectionId, out var roomCode))
            {
                presence.RemoveByConnection(Context.ConnectionId);

                var online = presence.GetOnlinePlayerIds(roomCode);

                await Clients.Group($"room:{roomCode}")
                    .SendAsync("PresenceUpdated", new
                    {
                        roomCode,
                        onlinePlayerIds = online
                    });
            }
            else
            {
                presence.RemoveByConnection(Context.ConnectionId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
