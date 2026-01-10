using Microsoft.AspNetCore.SignalR;
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
}
