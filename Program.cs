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
using testapi1.Services.Dialogue;
using testapi1.Services.Embeddings;
using testapi1.Services.Intent;
using testapi1.Services.Llm;
using testapi1.Services.Progression;
using testapi1.Services.Redis;
using testapi1.Services.Turns;
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
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection("Llm"));

builder.Services.Configure<OnnxModelOptions>(builder.Configuration.GetSection("Onnx"));
builder.Services.AddHttpClient("remote-dependency-probe");
builder.Services.AddHttpClient("llm-local");
builder.Services.AddHttpClient("llm-remote");
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

builder.Services.AddSingleton<ILLMService, LlmService>();
builder.Services.AddSingleton<IGameProgressionEngine, DylanProgressionEngine>();
builder.Services.AddScoped<IPlayerTurnResolver, PlayerTurnResolver>();
builder.Services.AddScoped<IProgressionSessionStore, PostgresProgressionSessionStore>();
builder.Services.AddScoped<IProgressionCatalogRepository, PostgresProgressionCatalogRepository>();
builder.Services.AddScoped<IProgressionRuntimeRepository, PostgresProgressionRuntimeRepository>();
builder.Services.AddScoped<IIntentToProgressionEventMapper, IntentToProgressionEventMapper>();
builder.Services.AddScoped<GameProgressionService>();
builder.Services.AddScoped<IGameProgressionService>(sp => sp.GetRequiredService<GameProgressionService>());
builder.Services.AddScoped<IResolvedProgressionTurnService>(sp => sp.GetRequiredService<GameProgressionService>());
builder.Services.AddScoped<IPlayerTurnOrchestrator, PlayerTurnOrchestrator>();
builder.Services.AddScoped<IRetrievalService, RetrievalService>();
builder.Services.AddScoped<NpcDialogueService>();
builder.Services.AddScoped<INpcDialogueService>(sp => sp.GetRequiredService<NpcDialogueService>());
builder.Services.AddScoped<IResolvedNpcDialogueService>(sp => sp.GetRequiredService<NpcDialogueService>());
// ---------- BUILD APP ----------

var app = builder.Build();

var redisTarget = DependencyTargetParser.GetRedisTarget(builder.Configuration.GetConnectionString("Redis"));
var qdrantTarget = DependencyTargetParser.GetQdrantTarget(builder.Configuration["Qdrant:BaseUrl"]);
var postgresTarget = DependencyTargetParser.GetPostgresTarget(builder.Configuration.GetConnectionString("Postgres"));
var llmOptionsSnapshot = builder.Configuration.GetSection("Llm").Get<LlmOptions>() ?? new LlmOptions();
app.Logger.LogInformation(
    "Dependency targets configured (host:port only). Redis={RedisTarget}; Qdrant={QdrantTarget}; Postgres={PostgresTarget}; DotEnvVariablesLoaded={DotEnvLoadedCount}",
    redisTarget,
    qdrantTarget,
    postgresTarget,
    dotEnvLoadedCount);
app.Logger.LogInformation(
    "LLM configuration loaded (host:port only). LocalEnabled={LocalEnabled}; LocalTarget={LocalTarget}; LocalModel={LocalModel}; RemoteEnabled={RemoteEnabled}; RemoteTarget={RemoteTarget}; RemoteModel={RemoteModel}",
    llmOptionsSnapshot.Local.Enabled,
    DescribeUrlTarget(llmOptionsSnapshot.Local.BaseUrl),
    string.IsNullOrWhiteSpace(llmOptionsSnapshot.Local.Model) ? "(unconfigured)" : llmOptionsSnapshot.Local.Model,
    llmOptionsSnapshot.Remote.Enabled,
    DescribeUrlTarget(llmOptionsSnapshot.Remote.BaseUrl),
    string.IsNullOrWhiteSpace(llmOptionsSnapshot.Remote.Model) ? "(unconfigured)" : llmOptionsSnapshot.Remote.Model);

app.UseCors("UnityCors");
app.UseSerilogRequestLogging();
app.UseAuthorization();

app.MapControllers();

app.Run();

static string DescribeUrlTarget(string? baseUrl)
{
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        return "(unconfigured)";
    }

    try
    {
        return new Uri(baseUrl, UriKind.Absolute).Authority;
    }
    catch (Exception)
    {
        return baseUrl.Trim();
    }
}
