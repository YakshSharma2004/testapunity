# testapi1

ASP.NET Core backend for the interrogation prototype. This branch uses:

- Postgres for authoritative progression/session data
- Qdrant for intent vectors
- Redis for cache
- ONNX embedding models from the repo `models/` folder
- an optional localhost LLM endpoint, with app-level fallback if no model is configured yet

This README is the fastest path from a fresh clone to a working local setup.

## Prerequisites

Install these first:

- .NET 8 SDK
- Docker Desktop with `docker compose`
- Python 3.10+ with `pip`
- Git

Optional but useful:

- Postman
- Ollama or another localhost LLM server, if you want to test the LLM path later

## 1. Clone The Repo

```powershell
git clone <YOUR_REPO_URL>
cd testapunity
```

## 2. Create Your Local `.env`

Copy the example file:

```powershell
Copy-Item .env.example .env
```

Then open `.env` and set at least these values:

- `POSTGRES_PASSWORD`
- `CONNECTIONSTRINGS__POSTGRES`
- `QDRANT__BASEURL`
- `QDRANT__COLLECTIONNAME`

For the default local Docker setup in this repo, these values should work after you replace the password:

```env
POSTGRES_PASSWORD=YOUR_POSTGRES_PASSWORD
POSTGRES_DB=testgame
POSTGRES_USER=postgres
CONNECTIONSTRINGS__REDIS=localhost:6379,abortConnect=false,connectRetry=5,connectTimeout=5000,syncTimeout=5000
CONNECTIONSTRINGS__POSTGRES=Host=localhost;Port=55432;Database=testgame;Username=postgres;Password=YOUR_POSTGRES_PASSWORD;Pooling=true;Timeout=5;Command Timeout=10
VECTORSTORE__PROVIDER=Qdrant
QDRANT__BASEURL=http://localhost:6333
QDRANT__COLLECTIONNAME=intent-seed-poc
QDRANT__APIKEY=
REDIS__INSTANCENAME=testapi1:
LLM__LOCAL__ENABLED=true
LLM__LOCAL__BASEURL=http://localhost:11434
LLM__LOCAL__MODEL=qwen2.5:3b
LLM__LOCAL__TIMEOUTMS=120000
LLM__LOCAL__SEED=17
LLM__LOCAL__MAXRECENTEXCHANGES=4
LLM__LOCAL__MAXLORESNIPPETS=3
LLM__LOCAL__MAXTIMELINEITEMS=3
LLM__LOCAL__MAXPUBLICSTORYCHARS=500
LLM__LOCAL__MAXTRUTHSUMMARYCHARS=500
LLM__LOCAL__MAXRELATIONSHIPMEMORYCHARS=200
LLM__REMOTE__ENABLED=false
LLM__REMOTE__BASEURL=https://api.openai.com/v1
LLM__REMOTE__MODEL=
LLM__REMOTE__APIKEY=
LLM__REMOTE__TIMEOUTMS=60000
LLM__GENERATION__MAXTOKENS=160
LLM__GENERATION__TEMPERATURE=0.20
REMOTECONNECTIVITY__TIMEOUTMS=2000
```

## 3. Install .NET And Python Dependencies

Restore the local .NET tool manifest and NuGet packages:

```powershell
dotnet tool restore
dotnet restore
```

Install the Python packages used by the seed scripts:

```powershell
python -m pip install --upgrade pip
python -m pip install psycopg2-binary numpy onnxruntime tokenizers
```

If you prefer a virtual environment:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
python -m pip install psycopg2-binary numpy onnxruntime tokenizers
```

## 4. Start Docker Dependencies

Start Postgres, Redis, and Qdrant:

```powershell
docker compose up -d postgres redis qdrant
```

Check that the containers are up:

```powershell
docker compose ps
```

The repo compose file exposes:

- Postgres on `localhost:55432`
- Redis on `localhost:6379`
- Qdrant on `localhost:6333`

## 5. Apply The Latest EF Core Migration

This repo already contains migrations. On a fresh clone you usually want to apply the latest migration, not create a new one:

```powershell
dotnet tool run dotnet-ef database update
```

Design-time EF commands read `CONNECTIONSTRINGS__POSTGRES` from your repo `.env`, so make sure it is set before running the command.

## 6. Seed Postgres

Seed the gameplay tables with the deterministic developer dataset:

```powershell
python scripts/seed_postgres.py --force-reset
```

Notes:

- This is destructive for the app tables listed in the script.
- It resets and reseeds the developer dataset.
- If you only want to preview what it will do:

```powershell
python scripts/seed_postgres.py --dry-run
```

## 7. Seed Qdrant Intent Vectors

Seed the intent examples into Qdrant using the same tokenizer and ONNX embedding path that the app uses at runtime:

```powershell
python scripts/seed_qdrant_intents.py
```

Useful variants:

```powershell
python scripts/seed_qdrant_intents.py --dry-run
python scripts/seed_qdrant_intents.py --model multiqa
```

What the script does:

- reads Qdrant and embedding settings from `.env` and appsettings
- loads `models/mpnet` or `models/multiqa` directly
- ensures the Qdrant collection exists
- seeds deterministic UUID intent points so reruns update instead of duplicating
- runs a small similarity smoke check at the end

## 8. Run The API

The simplest launch profile is the HTTP profile:

```powershell
dotnet run --launch-profile http
```

That starts the API on:

- `http://localhost:5000`

Swagger is available at:

- `http://localhost:5000/swagger`

If you prefer the HTTPS profile:

```powershell
dotnet run --launch-profile https
```

That profile uses:

- `https://localhost:7219`
- `http://localhost:5263`

## 9. Optional Verification

Check the dependency probe:

```text
GET /api/v1/infra/dependencies
```

Run tests:

```powershell
dotnet test .\testapi1.Tests\testapi1.Tests.csproj --no-restore -v minimal
```

## 10. Local LLM Status

The backend already supports a localhost-first LLM configuration through these env keys:

- `LLM__LOCAL__ENABLED`
- `LLM__LOCAL__BASEURL`
- `LLM__LOCAL__MODEL`
- `LLM__LOCAL__SEED`

For same-machine Ollama bring-up:

```powershell
ollama list
curl http://localhost:11434/v1/models
```

Recommended local env values:

```env
LLM__LOCAL__ENABLED=true
LLM__LOCAL__BASEURL=http://localhost:11434
LLM__LOCAL__MODEL=qwen2.5:3b
LLM__LOCAL__TIMEOUTMS=120000
LLM__LOCAL__SEED=17
LLM__GENERATION__MAXTOKENS=160
LLM__GENERATION__TEMPERATURE=0.20
```

The dependency probe now includes Ollama as a separate check and validates that the configured local model appears in `GET /v1/models`.

Later, if you move Ollama to a second laptop, only change:

```env
LLM__LOCAL__BASEURL=http://<second-laptop-ip>:11434
```

## Common Commands

Start dependencies:

```powershell
docker compose up -d postgres redis qdrant
```

Stop dependencies:

```powershell
docker compose down
```

Apply latest migration:

```powershell
dotnet tool run dotnet-ef database update
```

Seed Postgres:

```powershell
python scripts/seed_postgres.py --force-reset
```

Seed Qdrant:

```powershell
python scripts/seed_qdrant_intents.py
```

Run API:

```powershell
dotnet run --launch-profile http
```

Run tests:

```powershell
dotnet test .\testapi1.Tests\testapi1.Tests.csproj --no-restore -v minimal
```

## Troubleshooting

If `docker compose up` fails for Postgres:

- make sure `.env` contains `POSTGRES_PASSWORD`

If `dotnet-ef database update` says the Postgres connection string is missing:

- make sure `.env` contains `CONNECTIONSTRINGS__POSTGRES`

If `seed_postgres.py` fails:

- confirm Postgres is running on `localhost:55432`
- confirm the password in `CONNECTIONSTRINGS__POSTGRES` matches `POSTGRES_PASSWORD`
- confirm `psycopg2-binary` is installed

If `seed_qdrant_intents.py` fails:

- confirm Qdrant is running on `localhost:6333`
- confirm `numpy`, `onnxruntime`, and `tokenizers` are installed
- confirm the embedding model files exist under `models/mpnet` or `models/multiqa`

If the API starts but intent classification does not behave correctly:

- make sure Qdrant has been seeded
- make sure `VECTORSTORE__PROVIDER=Qdrant`

If Postman or the browser gives HTTPS certificate issues:

- use the `http` profile and `http://localhost:5000`
