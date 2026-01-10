using Microsoft.AspNetCore.Mvc;
using RTQuiz.Application.Games;
using RTQuiz.Domain.Games;
using Microsoft.AspNetCore.SignalR;
using RTQuiz.Api.Hubs;

namespace RTQuiz.Api.Controllers;

public sealed record JoinGameRequest(string PlayerName);
public sealed record JoinGameResponse(string PlayerId);
public sealed record CreateGameResponse(string RoomCode);
public sealed record GetGameResponse(string RoomCode, List<PlayerDto> Players);
public sealed record PlayerDto(string Id, string Name);

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

            // po TryJoin:
            store.TryGet(code, out var session); // albo trzymaj session w TryJoin, jeśli wolisz

            await hubContext.Clients
                .Group($"room:{code.Value}")
                .SendAsync("LobbyUpdated", new
                {
                    roomCode = code.Value,
                    players = session.Players.Select(p => new { id = p.Id, name = p.Name })
                });


            // 4) REST response: tylko playerId
            return Ok(new JoinGameResponse(player.Id));
        }
        catch (ArgumentException ex)
        {
            // np. walidacja playerName (2-20 znaków)
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{roomCode}")]
    public ActionResult<GetGameResponse> Get(string roomCode, [FromServices] IGameSessionStore store)
    {
        RoomCode code;
        try { code = RoomCode.From(roomCode); }
        catch { return NotFound(); }

        if (!store.TryGet(code, out var session))
            return NotFound();

        return Ok(new GetGameResponse(
            code.Value,
            session.Players.Select(p => new PlayerDto(p.Id, p.Name)).ToList()
        ));
    }
}