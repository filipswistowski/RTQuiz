using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RTQuiz.Api.Games;
using RTQuiz.Application.Games;
using RTQuiz.Domain.Games;

namespace RTQuiz.Api.Hubs;

public sealed class GameHub : Hub
{
    public async Task JoinRoom(
    string roomCode,
    [FromServices] IGameSessionStore store,
    [FromServices] IQuestionBank questionBank)
    {
        var code = RoomCode.From(roomCode);
        var groupName = $"room:{code.Value}";

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("JoinedRoom", code.Value);

        if (!store.TryGet(code, out var session))
            return;

        var snapshot = GameStateSyncBuilder.Build(session, questionBank);
        // StateSync is a "snapshot" event meant for (re)initializing the client's state after reconnect/refresh.
        // It also includes ServerNowUtcMs + QuestionEndsInMs so the frontend can compensate for network latency
        // and display a more accurate countdown timer.
        await Clients.Caller.SendAsync("StateSync", snapshot);
    }
}
