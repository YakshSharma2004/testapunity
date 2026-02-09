using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using testapi1.Application;   // interfaces namespace (adjust if needed)
using testapi1.Services;      // implementations namespace (adjust if needed)
using testapi1.Services.Caching;

var builder = WebApplication.CreateBuilder(args);

// Serilog (read config)
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    options.InstanceName = builder.Configuration["Redis:InstanceName"] ?? "testapi1:";
});

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
builder.Services.Configure<ApiCacheOptions>(builder.Configuration.GetSection("ApiCache"));
builder.Services.AddSingleton<IntentClassifier>();
builder.Services.AddSingleton<IIntentClassifier>(sp =>
    new CachedIntentClassifier(
        sp.GetRequiredService<IntentClassifier>(),
        sp.GetRequiredService<IDistributedCache>(),
        sp.GetRequiredService<ITextNormalizer>(),
        sp.GetRequiredService<IOptionsMonitor<ApiCacheOptions>>(),
        sp.GetRequiredService<ILogger<CachedIntentClassifier>>()));
builder.Services.AddSingleton<LlmService>();
builder.Services.AddSingleton<ILLMService>(sp =>
    new CachedLlmService(
        sp.GetRequiredService<LlmService>(),
        sp.GetRequiredService<IDistributedCache>(),
        sp.GetRequiredService<ITextNormalizer>(),
        sp.GetRequiredService<IOptionsMonitor<ApiCacheOptions>>(),
        sp.GetRequiredService<ILogger<CachedLlmService>>()));
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
