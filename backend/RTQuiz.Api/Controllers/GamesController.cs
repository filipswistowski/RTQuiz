using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RTQuiz.Api.Contracts;
using RTQuiz.Api.Games;
using RTQuiz.Api.Hubs;
using RTQuiz.Api.Services;
using RTQuiz.Application.Games;
using RTQuiz.Domain.Games;

namespace RTQuiz.Api.Controllers;

public sealed record JoinGameRequest(string PlayerName);
public sealed record JoinGameResponse(string PlayerId);
public sealed record CreateGameResponse(string RoomCode);
public sealed record GetGameResponse(string RoomCode, List<PlayerDto> Players);
public sealed record PlayerDto(string Id, string Name);
public sealed record StartGameResponse(string RoomCode);
public sealed record SubmitAnswerRequest(int AnswerIndex);
public sealed record SubmitAnswerResponse(string RoomCode);
public sealed record RevealResponse(string RoomCode, string QuestionId, int CorrectIndex);
public sealed record NextResponse(string RoomCode);

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

    [HttpPost("{roomCode}/start")]
    public async Task<ActionResult<StartGameResponse>> Start(
    string roomCode,
    [FromServices] IGameSessionStore store,
    [FromServices] IQuestionBank questionBank,
    [FromServices] IHubContext<GameHub> hubContext)
    {
        if (!Request.Headers.TryGetValue("X-Player-Id", out var playerIdValues))
            return BadRequest(new { error = "Missing X-Player-Id header." });

        var playerId = playerIdValues.ToString();

        RoomCode code;
        try { code = RoomCode.From(roomCode); }
        catch { return NotFound(); }

        if (!store.TryStart(code, playerId, out var session, out var error))
        {
            if (error == "NotFound")
                return NotFound();

            return BadRequest(new { error });
        }

        await hubContext.Clients
            .Group($"room:{code.Value}")
            .SendAsync("GameStarted", new { roomCode = code.Value });

        var questions = questionBank.GetAll();
        if (session.CurrentQuestionIndex >= 0 && session.CurrentQuestionIndex < questions.Count)
        {
            var q = questions[session.CurrentQuestionIndex];

            await hubContext.Clients
                .Group($"room:{code.Value}")
                .SendAsync("QuestionPresented", new
                {
                    questionId = q.Id,
                    text = q.Text,
                    answers = q.Answers
                });
        }

        return Ok(new StartGameResponse(code.Value));
    }

    [HttpPost("{roomCode}/answer")]
    public async Task<ActionResult<SubmitAnswerResponse>> Answer(
    string roomCode,
    [FromBody] SubmitAnswerRequest request,
    [FromServices] IGameSessionStore store,
    [FromServices] IQuestionBank questionBank,
    [FromServices] IHubContext<GameHub> hubContext)
    {
        if (!Request.Headers.TryGetValue("X-Player-Id", out var playerIdValues))
            return BadRequest(new { error = "Missing X-Player-Id header." });

        var playerId = playerIdValues.ToString();

        RoomCode code;
        try { code = RoomCode.From(roomCode); }
        catch { return NotFound(); }

        if (!store.TryGet(code, out var session))
            return NotFound();

        // Determine answersCount from the current question to validate answerIndex properly.
        var questions = questionBank.GetAll();

        if (session.CurrentQuestionIndex < 0 || session.CurrentQuestionIndex >= questions.Count)
            return BadRequest(new { error = "Invalid question index." });

        var q = questions[session.CurrentQuestionIndex];
        var answersCount = q.Answers.Count;

        if (!store.TrySubmitAnswer(code, playerId, request.AnswerIndex, answersCount, out session, out var error))
        {
            if (error == "NotFound") return NotFound();
            return BadRequest(new { error });
        }

        await hubContext.Clients
            .Group($"room:{code.Value}")
            .SendAsync("AnswerSubmitted", new { playerId });

        return Ok(new SubmitAnswerResponse(code.Value));
    }

    [HttpPost("{roomCode}/reveal")]
    public async Task<ActionResult<RevealResponse>> Reveal(
    string roomCode,
    [FromServices] IGameSessionStore store,
    [FromServices] IQuestionBank questionBank,
    [FromServices] IHubContext<GameHub> hubContext)
    {
        if (!Request.Headers.TryGetValue("X-Player-Id", out var playerIdValues))
            return BadRequest(new { error = "Missing X-Player-Id header." });

        var playerId = playerIdValues.ToString();

        RoomCode code;
        try { code = RoomCode.From(roomCode); }
        catch { return NotFound(); }

        if (!store.TryGet(code, out var session))
            return NotFound();

        var questions = questionBank.GetAll();
        if (session.CurrentQuestionIndex < 0 || session.CurrentQuestionIndex >= questions.Count)
            return BadRequest(new { error = "Invalid question index." });

        var q = questions[session.CurrentQuestionIndex];

        if (!store.TryReveal(code, playerId, q.CorrectIndex, out session, out var error))
        {
            if (error == "NotFound") return NotFound();
            return BadRequest(new { error });
        }

        await hubContext.Clients
            .Group($"room:{code.Value}")
            .SendAsync("AnswerRevealed", new { questionId = q.Id, correctIndex = q.CorrectIndex });

        var totalPlayers = session.Players.Count;
        var totalAnswered = session.CurrentAnswers.Count;

        var counts = new int[q.Answers.Count];
        foreach (var kv in session.CurrentAnswers)
        {
            var answerIndex = kv.Value;
            if (answerIndex >= 0 && answerIndex < counts.Length)
                counts[answerIndex]++;
        }

        var percentages = counts
            .Select(c => totalAnswered == 0 ? 0.0 : Math.Round((double)c * 100.0 / totalAnswered, 1))
            .ToArray();

        await hubContext.Clients
            .Group($"room:{code.Value}")
            .SendAsync("AnswerStatsRevealed", new
            {
                roomCode = code.Value,
                questionId = q.Id,
                totalPlayers,
                totalAnswered,
                counts,
                percentages
            });

        var scoresPayload = session.Players
            .Select(p => new
            {
                playerId = p.Id,
                name = p.Name,
                points = session.Scores.TryGetValue(p.Id, out var pts) ? pts : 0
            })
            .ToList();

        await hubContext.Clients
            .Group($"room:{code.Value}")
            .SendAsync("ScoreboardUpdated", new { roomCode = code.Value, scores = scoresPayload });

        return Ok(new RevealResponse(code.Value, q.Id, q.CorrectIndex));
    }

    [HttpPost("{roomCode}/next")]
    public async Task<ActionResult<NextResponse>> Next(
    string roomCode,
    [FromServices] IGameSessionStore store,
    [FromServices] IQuestionBank questionBank,
    [FromServices] IHubContext<GameHub> hubContext)
    {
        if (!Request.Headers.TryGetValue("X-Player-Id", out var playerIdValues))
            return BadRequest(new { error = "Missing X-Player-Id header." });

        var playerId = playerIdValues.ToString();

        RoomCode code;
        try { code = RoomCode.From(roomCode); }
        catch { return NotFound(); }

        var questions = questionBank.GetAll();
        var total = questions.Count;

        if (!store.TryNext(code, playerId, total, out var session, out var error))
        {
            if (error == "NotFound") return NotFound();
            return BadRequest(new { error });
        }

        // If we've just finished the game (no more questions), broadcast final scoreboard.
        if (session.Phase == GamePhase.Finished)
        {
            var finalScores = session.Players
                .Select(p => new
                {
                    playerId = p.Id,
                    name = p.Name,
                    points = session.Scores.TryGetValue(p.Id, out var pts) ? pts : 0
                })
                .OrderByDescending(x => x.points)
                .ToList();

            await hubContext.Clients
                .Group($"room:{code.Value}")
                .SendAsync("GameFinished", new
                {
                    roomCode = code.Value,
                    scores = finalScores
                });

            return Ok(new NextResponse(code.Value));
        }

        // Normal path: present next question
        if (session.CurrentQuestionIndex < 0 || session.CurrentQuestionIndex >= questions.Count)
            return BadRequest(new { error = "Invalid question index." });

        var q = questions[session.CurrentQuestionIndex];

        await hubContext.Clients
            .Group($"room:{code.Value}")
            .SendAsync("QuestionPresented", new
            {
                questionId = q.Id,
                text = q.Text,
                answers = q.Answers
            });

        return Ok(new NextResponse(code.Value));
    }

    [HttpGet("{roomCode}/state")]
    public ActionResult<GameStateSync> State(
    string roomCode,
    [FromServices] IGameSessionStore store,
    [FromServices] IQuestionBank questionBank,
    [FromServices] InMemoryPresenceStore presence
)
    {
        RoomCode code;
        try { code = RoomCode.From(roomCode); }
        catch { return NotFound(); }

        if (!store.TryGet(code, out var session))
            return NotFound();

        var snapshot = GameStateSyncBuilder.Build(session, questionBank, presence);
        return Ok(snapshot);
    }
}