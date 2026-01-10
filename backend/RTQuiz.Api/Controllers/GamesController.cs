using Microsoft.AspNetCore.Mvc;
using RTQuiz.Application.Games;
using RTQuiz.Domain.Games;

namespace RTQuiz.Api.Controllers;

public sealed record JoinGameRequest(string PlayerName);
public sealed record JoinGameResponse(string PlayerId);
public sealed record CreateGameResponse(string RoomCode);

[ApiController]
[Route("api/games")]
public class GamesController : ControllerBase
{
    [HttpPost]
    public ActionResult<CreateGameResponse> Create([FromServices] IGameSessionStore store)
    {
        var session = store.CreateNew();
        return Ok(new CreateGameResponse(session.RoomCode.Value));
    }

    [HttpPost("{roomCode}/join")]
    public ActionResult<JoinGameResponse> Join(string roomCode, [FromBody] JoinGameRequest request,
        [FromServices] IGameSessionStore store)
    {
        // Minimalnie: walidujemy długość/format przez Domain
        RoomCode code;
        try { code = RoomCode.From(roomCode); }
        catch { return NotFound(); }

        if (!store.Exists(code))
            return NotFound(); // 404 to standard dla “nie ma zasobu”. [web:521]

        var playerId = Guid.NewGuid().ToString("N");
        return Ok(new JoinGameResponse(playerId));
    }
}