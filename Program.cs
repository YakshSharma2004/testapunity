using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using testapi1.Application;
using testapi1.Infrastructure.VectorStores;
using testapi1.Infrastructure.VectorStores.Qdrant;
using testapi1.Services;
using testapi1.Services.Caching;
using testapi1.Services.Configuration;
using testapi1.Services.Connectivity;
using testapi1.Services.Embeddings;
using testapi1.Services.Intent;
using testapi1.Services.Progression;
using testapi1.Services.Redis;
using Microsoft.EntityFrameworkCore;
using testapi1.Infrastructure.Persistence;

var dotEnvLoadedCount = DotEnvLoader.LoadIfDevelopment(Directory.GetCurrentDirectory());
ExternalDependencyEnvValidator.ValidateOrThrow();
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
builder.Services.Configure<RemoteConnectivityOptions>(builder.Configuration.GetSection("RemoteConnectivity"));

builder.Services.Configure<OnnxModelOptions>(builder.Configuration.GetSection("Onnx"));
builder.Services.AddHttpClient("remote-dependency-probe");
builder.Services.AddSingleton<IRedisCacheStore, DistributedCacheRedisStore>();
builder.Services.AddSingleton<ITextNormalizer, TextNormalizationService>();
builder.Services.AddSingleton<IOnnxModelRunner, OnnxModelRunner>();
builder.Services.AddSingleton<IRemoteDependencyProbe, RemoteDependencyProbeService>();
builder.Services.AddSingleton<ConfigurableOnnxEmbeddingService>();
builder.Services.AddSingleton<IEmbeddingService>(sp => sp.GetRequiredService<ConfigurableOnnxEmbeddingService>());
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

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

builder.Services.AddSingleton<IntentClassifier>();

builder.Services.AddSingleton<IIntentClassifier>(sp =>
    new CachedIntentClassifier(
        sp.GetRequiredService<IntentClassifier>(),
        sp.GetRequiredService<IRedisCacheStore>(),
        sp.GetRequiredService<ITextNormalizer>(),
        sp.GetRequiredService<IOptionsMonitor<ApiCacheOptions>>(),
        sp.GetRequiredService<IOptionsMonitor<EmbeddingsOptions>>(),
        sp.GetRequiredService<ILogger<CachedIntentClassifier>>()));

builder.Services.AddSingleton<LlmService>();

builder.Services.AddSingleton<ILLMService>(sp => sp.GetRequiredService<LlmService>());
builder.Services.AddSingleton<IGameProgressionEngine, DylanProgressionEngine>();
builder.Services.AddScoped<IProgressionSessionStore, PostgresProgressionSessionStore>();
builder.Services.AddSingleton<IIntentToProgressionEventMapper, IntentToProgressionEventMapper>();
builder.Services.AddScoped<IGameProgressionService, GameProgressionService>();
// ---------- BUILD APP ----------

var app = builder.Build();

var redisTarget = DependencyTargetParser.GetRedisTarget(builder.Configuration.GetConnectionString("Redis"));
var qdrantTarget = DependencyTargetParser.GetQdrantTarget(builder.Configuration["Qdrant:BaseUrl"]);
var postgresTarget = DependencyTargetParser.GetPostgresTarget(builder.Configuration.GetConnectionString("Postgres"));
app.Logger.LogInformation(
    "Dependency targets configured (host:port only). Redis={RedisTarget}; Qdrant={QdrantTarget}; Postgres={PostgresTarget}; DotEnvVariablesLoaded={DotEnvLoadedCount}",
    redisTarget,
    qdrantTarget,
    postgresTarget,
    dotEnvLoadedCount);

app.UseCors("UnityCors");
app.UseSerilogRequestLogging();
app.UseAuthorization();

app.MapControllers();

app.Run();
