using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RTQuiz.Api.Services;
using RTQuiz.Domain.Games;

namespace RTQuiz.Api.Hubs;

public sealed class GameHub : Hub
{
    public async Task JoinRoom(string roomCode)
    {
        var code = RoomCode.From(roomCode);
        var groupName = $"room:{code.Value}";

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("JoinedRoom", code.Value);
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
        // remove presence first, then notify the room (if we can infer it)
        // Note: InMemoryPresenceStore stores mapping connectionId -> (roomCode, playerId)
        // so we can broadcast PresenceUpdated even on disconnect.
        var presence = Context.GetHttpContext()?.RequestServices.GetService<InMemoryPresenceStore>();
        if (presence is not null)
        {
            // we need roomCode before removal to broadcast correctly
            // easiest: call GetOnlinePlayerIds after removal for the same roomCode, so we capture mapping first
            // (store hides mapping, so use a small helper: temporarily re-Identify would be wrong).
            // We'll do a simple approach: store will remove, then we broadcast for all rooms is not OK,
            // so expose mapping here by adding TryGetRoomForConnection (below) OR do service method.

            // Minimal change: add this method to store (see section 4) and use it:
            if (presence.TryGetRoomForConnection(Context.ConnectionId, out var roomCode))
            {
                presence.RemoveByConnection(Context.ConnectionId);

                var online = presence.GetOnlinePlayerIds(roomCode);
                await Clients.Group($"room:{roomCode}")
                    .SendAsync("PresenceUpdated", new { roomCode, onlinePlayerIds = online });
            }
            else
            {
                presence.RemoveByConnection(Context.ConnectionId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
