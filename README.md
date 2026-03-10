# testapi1

## Redis container + API integration plan

1. Keep Redis optional so the API can run even when the container is stopped.
2. Use a resilient Redis connection string (`abortConnect=false`) so the API reconnects after Redis is started.
3. Provide a Docker Compose file dedicated to Redis (`docker-compose.redis.yml`) to allow on-demand start/stop.
4. Add a helper script (`scripts/redis-on-demand.sh`) for quick lifecycle control.
5. Register a placeholder Redis abstraction (`IRedisPlaceholderStore`) to anchor future Redis work (locks, presence, queues, etc.) without coupling new logic directly to `IDistributedCache`.

## Redis cache setup
This API uses Redis for caching intent and LLM responses. Configure the connection string via
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

The Redis compose file used by the script is `docker-compose.redis.yml` in the project root:

```yaml
services:
  redis:
    image: redis:7-alpine
    container_name: testapi1-redis
    command: ["redis-server", "--appendonly", "yes"]
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
docker compose -f docker-compose.redis.yml up -d redis
```

## Placeholder Redis work
A placeholder service is now registered:

- Interface: `Application/IRedisPlaceholderStore.cs`
- Implementation: `Services/Redis/DistributedCacheRedisPlaceholderStore.cs`

Use this for upcoming Redis-backed features so we can evolve behavior in one place (serialization, retry policy, key conventions, fallbacks).

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

Use `VectorStore:Provider = InMemory` for local POC and switch to `Qdrant` when cloud integration is ready.

### Embedding model selection

- Active runtime model is selected by `Embeddings:Model` (`mpnet` or `multiqa`).
- Model assets are loaded from the corresponding configured `ModelPath` + `TokenizerPath`.
- Keep paths lowercase (`models/...`) to avoid Linux path casing issues.

### Offline A/B evaluation CLI

Run a local comparison between configured embedding models using the same seed set and threshold policy:

```bash
dotnet run -- --eval-intents --dataset evaluation/intent-validation.json --models mpnet,multiqa --threshold 0.45 --sweep 0.35,0.45,0.55,0.65
```

Outputs are written to:

- `Logs/model-eval/<timestamp>-comparison.json`
- `Logs/model-eval/<timestamp>-comparison.md`

Validation dataset format (`evaluation/intent-validation.json`):

```json
[{ "text": "walk me through what happened", "expected_intent": "ASK_OPEN_QUESTION" }]
```
