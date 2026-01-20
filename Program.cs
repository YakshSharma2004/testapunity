using testapi1.models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapPost("/api/dialogue", (DialogueRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.playerText))
        return Results.BadRequest("playerText is required.");

    var reply = $"Echo Player '{req.playerId}' said: '{req.playerText}' " +
                $"to NPC '{req.npcId}' at '{req.inGameTime}'";

    return Results.Ok(new DialogueResponse
    {
        replyText = reply,
        conversationId = Guid.NewGuid().ToString("N")
    });
});


app.Run();
