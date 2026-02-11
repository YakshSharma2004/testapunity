using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using testapi1.Application;       // ITextNormalizer, IEmbeddingService, etc.
using testapi1.Infrastructure;    // IVectorStore, VectorDbStore, QdrantOptions
using testapi1.Services;          // TextNormalizationService, CachedIntentClassifier, etc.
using testapi1.Application;   // interfaces namespace (adjust if needed)

var builder = WebApplication.CreateBuilder(args);

// ---------- Serilog ----------
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

// ---------- MVC / API ----------
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

// ---------- CORS for Unity ----------
builder.Services.AddCors(options =>
{
    options.AddPolicy("UnityCors", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ---------- QDRANT + VECTOR STORE SETUP ----------

// Bind Qdrant options from appsettings.json ("Qdrant" section)
builder.Services.Configure<QdrantOptions>(
    builder.Configuration.GetSection("Qdrant"));

// Register HttpClient + VectorDbStore as the IVectorStore implementation
builder.Services.AddHttpClient<IVectorStore, VectorDbStore>();

// ---------- APPLICATION / SERVICES DI ----------

builder.Services.AddSingleton<ITextNormalizer, TextNormalizationService>();

builder.Services.Configure<ApiCacheOptions>(
    builder.Configuration.GetSection("ApiCache"));

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

builder.Services.Configure<OnnxModelOptions>(
    builder.Configuration.GetSection("Onnx"));

builder.Services.AddSingleton<IOnnxModelRunner, OnnxModelRunner>();
builder.Services.AddSingleton<IEmbeddingService, MpnetOnnxEmbeddingService>();

// Fake embeddings for now (no ONNX model required)
builder.Services.AddSingleton<IEmbeddingService, FakeEmbeddingService>();

// ---------- BUILD APP ----------

var app = builder.Build();


app.UseCors("UnityCors");
//dont use
// app.UseHttpsRedirection();

app.UseSerilogRequestLogging();
app.UseAuthorization();

app.MapControllers();

app.Run();
app.Start();
public sealed class QdrantOptions
{
    public string BaseUrl { get; set; } = "";
    public string CollectionName { get; set; } = "";
    public string? ApiKey { get; set; }

    public Uri GetBaseUri() => new(BaseUrl.TrimEnd('/') + "/");
}
