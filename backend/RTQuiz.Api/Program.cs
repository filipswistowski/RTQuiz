using RTQuiz.Api.Hubs;
using RTQuiz.Api.Services;
using RTQuiz.Application.Games;
using RTQuiz.Application.Games.CreateGame;
using RTQuiz.Infrastructure.Games;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddSingleton<IRoomCodeGenerator, RoomCodeGenerator>();
builder.Services.AddTransient<CreateGameService>();
builder.Services.AddSingleton<IGameSessionStore, InMemoryGameSessionStore>();
builder.Services.AddSingleton<IRoomCodeGenerator, RoomCodeGenerator>();
builder.Services.AddSingleton<IGameSessionStore, InMemoryGameSessionStore>();
builder.Services.AddSingleton<IQuestionBank, JsonQuestionBank>();
builder.Services.AddHostedService<QuestionTimerService>();

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

app.UseAuthorization();

app.UseRouting();

app.UseCors(cors);

app.MapControllers();
app.MapHub<GameHub>("/hubs/game");


app.Run();
