using System.Text.Json;
using Microsoft.Extensions.Hosting;
using RTQuiz.Application.Games;
using RTQuiz.Domain.Games;

namespace RTQuiz.Infrastructure.Games;

public sealed class JsonQuestionBank : IQuestionBank
{
    private readonly IReadOnlyList<Question> _questions;

    public JsonQuestionBank(IHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "questions.json");

        if (!File.Exists(path))
            throw new FileNotFoundException($"Questions file not found: {path}");

        var json = File.ReadAllText(path);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        _questions = JsonSerializer.Deserialize<List<Question>>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize questions.json.");
    }

    public IReadOnlyList<Question> GetAll() => _questions;
}
