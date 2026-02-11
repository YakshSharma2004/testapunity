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
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = builder.Configuration["Redis:InstanceName"] ?? "testapi1:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

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
builder.Services.AddSingleton<IEmbeddingService, MpnetOnnxEmbeddingService>();

var app = builder.Build();


app.UseCors("UnityCors");
//dont use
// app.UseHttpsRedirection();

app.UseSerilogRequestLogging();
app.UseAuthorization();

app.MapControllers();

app.Run();
