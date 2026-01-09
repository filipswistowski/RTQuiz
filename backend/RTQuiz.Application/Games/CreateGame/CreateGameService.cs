using RTQuiz.Domain.Games;

namespace RTQuiz.Application.Games.CreateGame;

public sealed class CreateGameService
{
    private readonly IRoomCodeGenerator _roomCodeGenerator;

    public CreateGameService(IRoomCodeGenerator roomCodeGenerator)
    {
        _roomCodeGenerator = roomCodeGenerator;
    }

    public CreateGameResult Create()
    {
        var code = _roomCodeGenerator.Generate();
        return new CreateGameResult(code);
    }
}

public sealed record CreateGameResult(RoomCode RoomCode);