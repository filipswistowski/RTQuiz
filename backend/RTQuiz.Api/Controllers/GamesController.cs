using Microsoft.AspNetCore.Mvc;
using RTQuiz.Application.Games.CreateGame;

namespace RTQuiz.Api.Controllers;

[ApiController]
[Route("api/games")]
public class GamesController : ControllerBase
{
    [HttpPost]
    public ActionResult<CreateGameResponse> Create([FromServices] CreateGameService service)
    {
        var result = service.Create();
        return Ok(new CreateGameResponse(result.RoomCode.Value));
    }
}

public sealed record CreateGameResponse(string RoomCode);