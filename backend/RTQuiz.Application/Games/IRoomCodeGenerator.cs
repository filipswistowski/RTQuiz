using RTQuiz.Domain.Games;

namespace RTQuiz.Application.Games;

public interface IRoomCodeGenerator
{
    RoomCode Generate();
}