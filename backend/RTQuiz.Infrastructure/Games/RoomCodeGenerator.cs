using System.Security.Cryptography;
using RTQuiz.Application.Games;
using RTQuiz.Domain.Games;

namespace RTQuiz.Infrastructure.Games;

public sealed class RoomCodeGenerator : IRoomCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public RoomCode Generate()
    {
        Span<byte> bytes = stackalloc byte[RoomCode.Length];
        RandomNumberGenerator.Fill(bytes);

        Span<char> chars = stackalloc char[RoomCode.Length];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return RoomCode.From(new string(chars));
    }
}