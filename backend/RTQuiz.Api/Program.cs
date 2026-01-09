using RTQuiz.Application.Games;
using RTQuiz.Application.Games.CreateGame;
using RTQuiz.Infrastructure.Games;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IRoomCodeGenerator, RoomCodeGenerator>();
builder.Services.AddTransient<CreateGameService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Redirect("/openapi/v1.json"));

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
