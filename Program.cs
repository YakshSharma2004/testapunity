using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using testapi1.Application;
using testapi1.Infrastructure.VectorStores;
using testapi1.Infrastructure.VectorStores.Qdrant;
using testapi1.Services;
using testapi1.Services.Caching;
using testapi1.Services.Intent;
using testapi1.Services.Redis;
var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddCors(options =>
{
    options.AddPolicy("UnityCors", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.Configure<ApiCacheOptions>(builder.Configuration.GetSection("ApiCache"));
builder.Services.Configure<IntentClassificationOptions>(builder.Configuration.GetSection("IntentClassification"));

builder.Services.Configure<OnnxModelOptions>(builder.Configuration.GetSection("Onnx"));
builder.Services.AddSingleton<IRedisPlaceholderStore, DistributedCacheRedisPlaceholderStore>();
builder.Services.AddSingleton<ITextNormalizer, TextNormalizationService>();
builder.Services.AddSingleton<IOnnxModelRunner, OnnxModelRunner>();
builder.Services.AddSingleton<IEmbeddingService, MpnetOnnxEmbeddingService>();

var vectorProvider = builder.Configuration["VectorStore:Provider"] ?? "InMemory";
if (string.Equals(vectorProvider, "Qdrant", StringComparison.OrdinalIgnoreCase))
{
    var qdrantBaseUrl = builder.Configuration["Qdrant:BaseUrl"];
    var qdrantCollection = builder.Configuration["Qdrant:CollectionName"];
    var qdrantApiKey = builder.Configuration["Qdrant:ApiKey"];

    if (string.IsNullOrWhiteSpace(qdrantBaseUrl) || string.IsNullOrWhiteSpace(qdrantCollection))
    {
        throw new InvalidOperationException("Qdrant provider selected but Qdrant:BaseUrl or Qdrant:CollectionName is missing.");
    }

    var qdrantOptions = new QdrantOptions(qdrantBaseUrl, qdrantCollection, qdrantApiKey);

    builder.Services.AddSingleton(qdrantOptions);
    builder.Services.AddHttpClient<IVectorStore, VectorDbStore>(client =>
    {
        client.BaseAddress = qdrantOptions.GetBaseUri();

        var apiKey = builder.Configuration["Qdrant:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Add("api-key", apiKey);
        }
    });
}
else
{
    builder.Services.AddSingleton<IVectorStore, InMemoryVectorStore>();
}

builder.Services.AddHostedService<IntentSeedHostedService>();
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
//this needs to be double chechked
builder.Services.Configure<OnnxModelOptions>(
    builder.Configuration.GetSection("Onnx"));

builder.Services.AddSingleton<IOnnxModelRunner, OnnxModelRunner>();
builder.Services.AddSingleton<IEmbeddingService, MpnetOnnxEmbeddingService>();

//// Fake embeddings for now (no ONNX model required)
//builder.Services.AddSingleton<IEmbeddingService, MpnetOnnxEmbeddingService>();

// ---------- BUILD APP ----------

var app = builder.Build();

app.UseCors("UnityCors");
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
    public QdrantOptions(string baseUrl, string collectionName, string? apiKey = null)
    {
        BaseUrl = baseUrl;
        CollectionName = collectionName;
        ApiKey = apiKey;
    }
    public Uri GetBaseUri() => new(BaseUrl.TrimEnd('/') + "/");
}
