using RTQuiz.Api.Hubs;
using RTQuiz.Api.Options;
using RTQuiz.Api.Services;
using RTQuiz.Application.Games;
using RTQuiz.Application.Games.CreateGame;
using RTQuiz.Infrastructure.Games;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("AnswerPerPlayerPerRoom", httpContext =>
    {
        // roomCode bierzemy z routingu: /api/games/{roomCode}/answer
        var roomCode = httpContext.Request.RouteValues.TryGetValue("roomCode", out var rv)
            ? rv?.ToString()
            : null;

        // playerId z headera, bo u Ciebie tak dzia³a autoryzacja gracza
        var playerId = httpContext.Request.Headers.TryGetValue("X-Player-Id", out var pv)
            ? pv.ToString()
            : null;

        // Jeœli nie mamy klucza, to fallback, ¿eby limiter nadal dzia³a³ (np. boty bez headerów).
        var key = (!string.IsNullOrWhiteSpace(roomCode) && !string.IsNullOrWhiteSpace(playerId))
            ? $"{roomCode}:{playerId}"
            : $"fallback:{httpContext.Connection.RemoteIpAddress}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 3,
                Window = TimeSpan.FromSeconds(2),
                QueueLimit = 0
            });
    });
});

builder.Services.AddSingleton<IRoomCodeGenerator, RoomCodeGenerator>();
builder.Services.AddTransient<CreateGameService>();
builder.Services.AddSingleton<IRoomCodeGenerator, RoomCodeGenerator>();
builder.Services.AddSingleton<IQuestionBank, JsonQuestionBank>();
builder.Services.AddHostedService<QuestionTimerService>();
builder.Services.AddHostedService<SessionCleanupService>();
builder.Services.AddSingleton<InMemoryGameSessionStore>();
builder.Services.AddSingleton<IGameSessionStore>(sp => sp.GetRequiredService<InMemoryGameSessionStore>());
builder.Services.AddSingleton<InMemoryPresenceStore>();


builder.Services
    .AddOptions<SessionCleanupOptions>()
    .BindConfiguration(SessionCleanupOptions.SectionName);

builder.Services
    .AddOptions<SessionCleanupOptions>()
    .BindConfiguration(SessionCleanupOptions.SectionName)
    .Validate(o =>
        o.TickSeconds is >= 5 and <= 3600 &&
        o.LobbyTtlMinutes is >= 1 and <= 24 * 60 &&
        o.InProgressTtlMinutes is >= 1 and <= 24 * 60,
        "Invalid SessionCleanup options.")
    .ValidateOnStart();

var cors = "Frontend";

builder.Services.AddCors(o =>
{
    o.AddPolicy(cors, p => p
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Redirect("/openapi/v1.json"));

app.UseHttpsRedirection();

app.UseRouting();
app.UseCors(cors);

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>("/hubs/game");

app.Run();