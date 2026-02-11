# testapi1

## Redis cache setup
This API uses Redis for caching intent and LLM responses. Configure the connection string via
`ConnectionStrings:Redis` and optionally set a key prefix with `Redis:InstanceName`. Defaults
are included in `appsettings.json` for local development.

### Local Redis with Docker
```bash
docker run --name testapi1-redis -p 6379:6379 -d redis:7
```

If you need to connect to a different host/port, update `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  },
  "Redis": {
    "InstanceName": "testapi1:"
  }
}
```

## ONNX Runtime integration (no Python runtime needed)

This service is set up to load and run ONNX models directly in .NET using **Microsoft.ML.OnnxRuntime**. You only need to export your Python model to `.onnx`, drop it into the repo (or a mounted volume), and point the app to the file. The rest of the preprocessing and postprocessing stays in .NET.

### 1) Configure the model path + I/O names

Update `appsettings.json` (or environment variables) with your model location and tensor names:

```json
"Onnx": {
  "ModelPath": "Models/intent-classifier.onnx",
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
