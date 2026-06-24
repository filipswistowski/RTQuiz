using System;
using Xunit;
using RTQuiz.Domain.Games;

namespace RTQuiz.Tests;

public class GameSessionTests
{
    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        var code = RoomCode.From("ABC123");
        var session = new GameSession(code, DateTime.UtcNow);

        Assert.Equal(code, session.RoomCode);
        Assert.Equal(GamePhase.Lobby, session.Phase);
        Assert.Equal(-1, session.CurrentQuestionIndex);
        Assert.False(session.IsQuestionOpen);
        Assert.Null(session.HostPlayerId);
        Assert.Empty(session.Players);
    }

    [Fact]
    public void AddPlayer_ShouldSetFirstPlayerAsHost()
    {
        var session = new GameSession(RoomCode.From("ABC123"), DateTime.UtcNow);

        var player1 = session.AddPlayer("PlayerOne");
        var player2 = session.AddPlayer("PlayerTwo");

        Assert.Equal(player1.Id, session.HostPlayerId);
        Assert.Equal(2, session.Players.Count);
        Assert.Contains(player1, session.Players);
        Assert.Contains(player2, session.Players);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("VeryLongPlayerNameThatExceedsLimit")]
    public void AddPlayer_ShouldThrowForInvalidNameLength(string invalidName)
    {
        var session = new GameSession(RoomCode.From("ABC123"), DateTime.UtcNow);

        Assert.Throws<ArgumentException>(() => session.AddPlayer(invalidName));
    }

    [Fact]
    public void StartGame_ShouldChangePhaseAndPresentFirstQuestion()
    {
        var session = new GameSession(RoomCode.From("ABC123"), DateTime.UtcNow);
        var host = session.AddPlayer("HostPlayer");

        session.StartGame(host.Id);

        Assert.Equal(GamePhase.InProgress, session.Phase);
        Assert.Equal(0, session.CurrentQuestionIndex);
        Assert.True(session.IsQuestionOpen);
        Assert.NotNull(session.QuestionOpenedAtUtc);
    }

    [Fact]
    public void StartGame_ShouldThrowIfNotHost()
    {
        var session = new GameSession(RoomCode.From("ABC123"), DateTime.UtcNow);
        var host = session.AddPlayer("HostPlayer");
        var guest = session.AddPlayer("GuestPlayer");

        Assert.Throws<InvalidOperationException>(() => session.StartGame(guest.Id));
    }

    [Fact]
    public void SubmitAnswer_ShouldRecordAnswer()
    {
        var session = new GameSession(RoomCode.From("ABC123"), DateTime.UtcNow);
        var host = session.AddPlayer("HostPlayer");
        session.StartGame(host.Id);

        session.SubmitAnswer(host.Id, 1, 4);

        Assert.Single(session.CurrentAnswers);
        Assert.Equal(1, session.CurrentAnswers[host.Id]);
    }

    [Fact]
    public void SubmitAnswer_ShouldOverwriteExistingAnswer()
    {
        var session = new GameSession(RoomCode.From("ABC123"), DateTime.UtcNow);
        var host = session.AddPlayer("HostPlayer");
        session.StartGame(host.Id);

        session.SubmitAnswer(host.Id, 1, 4);
        session.SubmitAnswer(host.Id, 2, 4);

        Assert.Equal(2, session.CurrentAnswers[host.Id]);
    }

    [Fact]
    public void RevealAnswerAndScore_ShouldIncrementScoresForCorrectAnswers()
    {
        var session = new GameSession(RoomCode.From("ABC123"), DateTime.UtcNow);
        var host = session.AddPlayer("HostPlayer");
        var guest = session.AddPlayer("GuestPlayer");
        session.StartGame(host.Id);

        // Host answers correctly (index 1), Guest answers incorrectly (index 2)
        session.SubmitAnswer(host.Id, 1, 4);
        session.SubmitAnswer(guest.Id, 2, 4);

        session.RevealAnswerAndScore(1);

        Assert.False(session.IsQuestionOpen);
        Assert.Equal(1, session.Scores[host.Id]);
        Assert.Equal(0, session.Scores[guest.Id]);
    }

    [Fact]
    public void NextQuestion_ShouldAdvanceIndexOrFinishGame()
    {
        var session = new GameSession(RoomCode.From("ABC123"), DateTime.UtcNow);
        var host = session.AddPlayer("HostPlayer");
        session.StartGame(host.Id);

        session.RevealAnswerAndScore(1);

        // Move to next question (total questions = 2)
        session.NextQuestion(host.Id, 2);

        Assert.Equal(1, session.CurrentQuestionIndex);
        Assert.True(session.IsQuestionOpen);
        Assert.Empty(session.CurrentAnswers);

        session.RevealAnswerAndScore(0);

        // Next question should trigger finish since index 2 >= total questions 2
        session.NextQuestion(host.Id, 2);

        Assert.Equal(GamePhase.Finished, session.Phase);
    }
}
