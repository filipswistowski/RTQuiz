using RTQuiz.Domain.Games;

namespace RTQuiz.Application.Games;

public interface IQuestionBank
{
    IReadOnlyList<Question> GetAll();
}
