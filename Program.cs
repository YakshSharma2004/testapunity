using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using testapi1.Application;
using testapi1.Infrastructure.VectorStores;
using testapi1.Infrastructure.VectorStores.Qdrant;
using testapi1.Services;
using testapi1.Services.Caching;
using testapi1.Services.Embeddings;
using testapi1.Services.Intent;
using testapi1.Services.Progression;
using testapi1.Services.Redis;
using Microsoft.EntityFrameworkCore;
using testapi1.Infrastructure.Persistence;

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
builder.Services.Configure<EmbeddingsOptions>(builder.Configuration.GetSection("Embeddings"));
builder.Services.Configure<ProgressionOptions>(builder.Configuration.GetSection("Progression"));
builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection("Qdrant"));

builder.Services.Configure<OnnxModelOptions>(builder.Configuration.GetSection("Onnx"));
builder.Services.AddSingleton<IRedisPlaceholderStore, DistributedCacheRedisPlaceholderStore>();
builder.Services.AddSingleton<ITextNormalizer, TextNormalizationService>();
builder.Services.AddSingleton<IOnnxModelRunner, OnnxModelRunner>();
builder.Services.AddSingleton<ConfigurableOnnxEmbeddingService>();
builder.Services.AddSingleton<IEmbeddingService>(sp => sp.GetRequiredService<ConfigurableOnnxEmbeddingService>());

var vectorProvider = builder.Configuration["VectorStore:Provider"] ?? "InMemory";
if (string.Equals(vectorProvider, "Qdrant", StringComparison.OrdinalIgnoreCase))
{
    var qdrantBaseUrl = builder.Configuration["Qdrant:BaseUrl"];
    var qdrantCollection = builder.Configuration["Qdrant:CollectionName"];

    if (string.IsNullOrWhiteSpace(qdrantBaseUrl) || string.IsNullOrWhiteSpace(qdrantCollection))
    {
        throw new InvalidOperationException("Qdrant provider selected but Qdrant:BaseUrl or Qdrant:CollectionName is missing.");
    }

    builder.Services.AddHttpClient<IVectorStore, VectorDbStore>((sp, client) =>
    {
        var qdrantOptions = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
        if (string.IsNullOrWhiteSpace(qdrantOptions.BaseUrl) || string.IsNullOrWhiteSpace(qdrantOptions.CollectionName))
        {
            throw new InvalidOperationException("Qdrant provider selected but Qdrant options are not configured.");
        }

        client.BaseAddress = qdrantOptions.GetBaseUri();

        if (!string.IsNullOrWhiteSpace(qdrantOptions.ApiKey))
        {
            client.DefaultRequestHeaders.Add("api-key", qdrantOptions.ApiKey);
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
        sp.GetRequiredService<IOptionsMonitor<EmbeddingsOptions>>(),
        sp.GetRequiredService<ILogger<CachedIntentClassifier>>()));

builder.Services.AddSingleton<LlmService>();

builder.Services.AddSingleton<ILLMService>(sp =>
    new CachedLlmService(
        sp.GetRequiredService<LlmService>(),
        sp.GetRequiredService<IDistributedCache>(),
        sp.GetRequiredService<ITextNormalizer>(),
        sp.GetRequiredService<IOptionsMonitor<ApiCacheOptions>>(),
        sp.GetRequiredService<ILogger<CachedLlmService>>()));
builder.Services.AddSingleton<IGameProgressionEngine, DylanProgressionEngine>();
builder.Services.AddSingleton<IProgressionSessionStore, InMemoryProgressionSessionStore>();
builder.Services.AddSingleton<IIntentToProgressionEventMapper, IntentToProgressionEventMapper>();
builder.Services.AddSingleton<IGameProgressionService, GameProgressionService>();

// ---------- Postgres ----------

var postgresConnection = builder.Configuration.GetConnectionString("Postgres");
if (!string.IsNullOrWhiteSpace(postgresConnection))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(postgresConnection));

    // Replace InMemoryProgressionSessionStore with Postgres-backed store
    builder.Services.AddScoped<IProgressionSessionStore, PostgresProgressionSessionStore>();
}
else
{
    // Fallback to in-memory if no Postgres connection configured
    builder.Services.AddSingleton<IProgressionSessionStore, InMemoryProgressionSessionStore>();
}
// ---------- BUILD APP ----------

var app = builder.Build();

app.UseCors("UnityCors");
app.UseSerilogRequestLogging();
app.UseAuthorization();

app.MapControllers();

app.Run();
