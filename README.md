# testapi1

## Remote-first connectivity (Laptop 1 -> Laptop 2)

This API now supports remote-first dependency targets for Redis, Qdrant, and Postgres, with manual fallback via environment variables.

- Redis cache path is controlled by `ConnectionStrings:Redis`.
- Qdrant vector path remains `IVectorStore` (`VectorStore:Provider=Qdrant`) and is controlled by `Qdrant:BaseUrl`.
- Postgres is connectivity-probe only in this phase, controlled by `ConnectionStrings:Postgres`.
- A dependency probe endpoint is available at `GET /api/v1/infra/dependencies`.
- In `Development`, a `.env` file in the repo root is auto-loaded at startup (without overriding already-set environment variables).
- External dependency endpoints are env-first and validated at startup (`.env` or process environment variables).
- Intent caching uses SHA-256 hashed cache keys (no raw normalized text in Redis key names).
- LLM caching is intentionally disabled for now.
- Redis runs with memory protection and eviction: `maxmemory=1gb`, `maxmemory-policy=allkeys-lru`.

### Main API .env example (Laptop 1)

Use `.env.example` as the template for remote-first settings and local fallback switch values.
Required keys are:
- `CONNECTIONSTRINGS__REDIS`
- `CONNECTIONSTRINGS__POSTGRES`
- `VECTORSTORE__PROVIDER` (must be `Qdrant`)
- `QDRANT__BASEURL`
- `QDRANT__COLLECTIONNAME`

### Laptop 2 Docker .env example

Use `.env.laptop2.example` as the template for Docker Compose secrets/credentials on Laptop 2.

## Redis container + API integration plan

1. Keep Redis optional so the API can run even when the container is stopped.
2. Use a resilient Redis connection string (`abortConnect=false`) so the API reconnects after Redis is started.
3. Run Redis using the repo compose file (`docker-compose.yml`) for on-demand start/stop.
4. Add a helper script (`scripts/redis-on-demand.sh`) for quick lifecycle control.
5. Use `IRedisCacheStore` as the single Redis cache abstraction for app services.

## Redis cache setup
This API uses Redis for intent caching. Configure the connection string via
`ConnectionStrings:Redis` and optionally set a key prefix with `Redis:InstanceName`.

Current defaults are resilient to Redis being offline at API startup:

```json
"ConnectionStrings": {
  "Redis": "localhost:6379,abortConnect=false,connectRetry=5,connectTimeout=5000,syncTimeout=5000"
}
```

## Run Redis on-demand with Docker Compose

### Start
```bash
./scripts/redis-on-demand.sh start
```

### Check status
```bash
./scripts/redis-on-demand.sh status
```

### View logs
```bash
./scripts/redis-on-demand.sh logs
```

### Stop (keep data volume)
```bash
./scripts/redis-on-demand.sh stop
```

### Remove container/network
```bash
./scripts/redis-on-demand.sh down
```

> Redis data is persisted in the `redis_data` Docker volume.

## Exactly where the script is

The script is in this repo at:

- `scripts/redis-on-demand.sh`

From the project root (`/workspace/testapunity`), run:

```bash
chmod +x ./scripts/redis-on-demand.sh
./scripts/redis-on-demand.sh start
```

If you are in a different folder, call it with an absolute path:

```bash
/workspace/testapunity/scripts/redis-on-demand.sh start
```

## Docker Compose file (copy/paste)

Redis service runs from `docker-compose.yml` in the project root:

```yaml
services:
  redis:
    image: redis:7-alpine
    container_name: testapi1-redis
    command:
      [
        "redis-server",
        "--appendonly",
        "yes",
        "--maxmemory",
        "${REDIS_MAXMEMORY:-1gb}",
        "--maxmemory-policy",
        "${REDIS_MAXMEMORY_POLICY:-allkeys-lru}"
      ]
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 10
    restart: unless-stopped

volumes:
  redis_data:
```

You can also run Docker Compose directly without the helper script:

```bash
docker compose -f docker-compose.yml up -d redis
```

## Redis cache abstraction
The Redis cache abstraction is:

- Interface: `Application/IRedisCacheStore.cs`
- Implementation: `Services/Redis/DistributedCacheRedisStore.cs`

Use this abstraction for Redis-backed features so behavior stays centralized (serialization, retry policy, key conventions, fallbacks).

## ONNX Runtime integration (no Python runtime needed)

This service is set up to load and run ONNX models directly in .NET using **Microsoft.ML.OnnxRuntime**. You only need to export your Python model to `.onnx`, drop it into the repo (or a mounted volume), and point the app to the file. The rest of the preprocessing and postprocessing stays in .NET.

### 1) Configure the model path + I/O names

Update `appsettings.json` (or environment variables) with your model location and tensor names:

```json
"Onnx": {
  "ModelPath": "models/intent-classifier.onnx",
  "InputNames": [ "input_ids" ],
  "OutputNames": [ "logits" ]
}
```

Notes:
- `ModelPath` can be relative to the app root or an absolute path.
- `InputNames` and `OutputNames` should match the names shown by the exporter or a Netron model viewer.

### 2) How inference is wired

The API registers an `IOnnxModelRunner` service. This singleton loads the ONNX model the first time you call it and keeps the session warm for reuse.

Key points:
- It accepts a dictionary of input tensors (so you control preprocessing).
- It returns a dictionary of output tensors (so you control postprocessing).
- It throws clear errors if the model path is missing or if outputs are not `float` tensors.

### 3) Minimal example (inside a service/controller)

```csharp
var inputs = new Dictionary<string, Tensor<float>>
{
    ["input_ids"] = new DenseTensor<float>(inputIds, new[] { 1, inputIds.Length })
};

var outputs = await _onnxModelRunner.RunAsync(inputs, cancellationToken);
var logits = outputs["logits"];
```

### 4) Preprocessing & postprocessing tips

- **Preprocessing:** tokenization, padding, and any normalization should happen in .NET before you call `RunAsync`.
- **Postprocessing:** softmax, argmax, thresholds, label mapping, etc. should also live in .NET.
- **Model parity:** always compare outputs against your Python baseline to confirm identical behavior.

### 5) GPU (optional later)

If you want GPU acceleration, swap the runtime package to the GPU variant and update `SessionOptions` accordingly. Start with CPU first to validate correctness.

## Intent classification POC (seeded cosine similarity)

This project now supports a seeded intent classification proof-of-concept:

- On startup, a hosted service embeds fixed seed utterances and upserts them into the configured `IVectorStore`.
- The classifier embeds user text, runs top-k similarity search, and picks the highest-scoring intent.
- A confidence threshold allows fallback to `unknown` for low-similarity queries.

### Configuration

```json
"Embeddings": {
  "Model": "mpnet",
  "Models": {
    "mpnet": {
      "ModelPath": "models/mpnet/model.onnx",
      "TokenizerPath": "models/mpnet/tokenizer.json",
      "MaxLen": 384
    },
    "multiqa": {
      "ModelPath": "models/multiqa/model.onnx",
      "TokenizerPath": "models/multiqa/tokenizer.json",
      "MaxLen": 384
    }
  }
},
"IntentClassification": {
  "TopK": 3,
  "MinConfidence": 0.45,
  "IncludeDebugNotes": true
},
"VectorStore": {
  "Provider": "InMemory"
},
"Qdrant": {
  "BaseUrl": "https://your-qdrant-instance.cloud.qdrant.io",
  "CollectionName": "intent-seed-poc",
  "ApiKey": ""
}
```

Set `VectorStore:Provider` via environment variables and keep it as `Qdrant` for this env-first setup.
For Qdrant API key, prefer environment variable `Qdrant__ApiKey` instead of committing secrets.

### Embedding model selection

- Active runtime model is selected by `Embeddings:Model` (`mpnet` or `multiqa`).
- Model assets are loaded from the corresponding configured `ModelPath` + `TokenizerPath`.
- Keep paths lowercase (`models/...`) to avoid Linux path casing issues.

### Progression state machine API (backend authoritative)

Progression endpoints:

- `POST /api/v1/progression/start`
- `POST /api/v1/progression/turn`
- `GET /api/v1/progression/{sessionId}`

The progression engine currently ships with one authored case graph (`dylan-interrogation`) and uses:

- intent classification output (`ASK_TIMELINE`, `PRESENT_EVIDENCE`, etc.),
- evidence detection from evidence-style turns (`E1`, `E2`, `E4`, `E5`, `E7`),
- deterministic transition rules with terminal endings.

Session state is stored in-memory with TTL configured by:

```json
"Progression": {
  "SessionTtlMinutes": 120
}
```
