namespace RTQuiz.Domain.Games;

public readonly record struct RoomCode(string Value)
{
    public const int Length = 6;

    public override string ToString() => Value;

    public static RoomCode From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("RoomCode cannot be empty.", nameof(value));

        value = value.Trim().ToUpperInvariant();

        if (value.Length != Length)
            throw new ArgumentException($"RoomCode must be {Length} characters.", nameof(value));

        return new RoomCode(value);
    }
}