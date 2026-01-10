using Microsoft.AspNetCore.Mvc;
using RTQuiz.Application.Games;
using RTQuiz.Domain.Games;
using Microsoft.AspNetCore.SignalR;
using RTQuiz.Api.Hubs;

namespace RTQuiz.Api.Controllers;

public sealed record JoinGameRequest(string PlayerName);
public sealed record JoinGameResponse(string PlayerId);
public sealed record CreateGameResponse(string RoomCode);

[ApiController]
[Route("api/games")]
public class GamesController : ControllerBase
{
    [HttpPost]
    public ActionResult<CreateGameResponse> Create([FromServices] IGameSessionStore store, [FromServices] IHubContext<GameHub> hubContext)
    {
        var session = store.CreateNew();
        return Ok(new CreateGameResponse(session.RoomCode.Value));
    }

    [HttpPost("{roomCode}/join")]
    public async Task<ActionResult<JoinGameResponse>> Join(
    string roomCode,
    [FromBody] JoinGameRequest request,
    [FromServices] IGameSessionStore store,
    [FromServices] IHubContext<GameHub> hubContext)
    {
        // 1) Walidacja roomCode (błędny format traktujemy jako "nie ma takiego pokoju")
        RoomCode code;
        try
        {
            code = RoomCode.From(roomCode);
        }
        catch
        {
            return NotFound();
        }

        // 2) Join do sesji (Twoje store ma to dopisać do GameSession i zwrócić Player)
        try
        {
            if (!store.TryJoin(code, request.PlayerName, out var player))
                return NotFound();

            // 3) Broadcast do wszystkich, którzy są podłączeni do SignalR group dla tego pokoju
            await hubContext.Clients
                .Group($"room:{code.Value}")
                .SendAsync("PlayerJoined", new { playerId = player.Id, playerName = player.Name }); // [web:593]

            // 4) REST response: tylko playerId
            return Ok(new JoinGameResponse(player.Id));
        }
        catch (ArgumentException ex)
        {
            // np. walidacja playerName (2-20 znaków)
            return BadRequest(new { error = ex.Message });
        }
    }
}