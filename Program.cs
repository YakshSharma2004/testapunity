using Serilog;
using testapi1.Application;   // interfaces namespace (adjust if needed)
using testapi1.processes;
using testapi1.Services;      // implementations namespace (adjust if needed)

var builder = WebApplication.CreateBuilder(args);

// Serilog (read config)
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("UnityCors", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// DI: interface in Application, implementation in Services
builder.Services.AddSingleton<ITextNormalizer, TextNormalizationService>();
builder.Services.AddSingleton<IIntentClassifier, IntentClassifier>();
builder.Services.AddSingleton<ILLMService, LlmService>();
builder.Services.Configure<OnnxModelOptions>(builder.Configuration.GetSection("Onnx"));
builder.Services.AddSingleton<IOnnxModelRunner, OnnxModelRunner>();

var app = builder.Build();

// IMPORTANT: CORS must be before MapControllers
app.UseCors("UnityCors");

// For LAN dev: DO NOT force https redirect (it often breaks Unity calls)
// app.UseHttpsRedirection();

app.UseSerilogRequestLogging();
app.UseAuthorization();

app.MapControllers();

app.Run();
