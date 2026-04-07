# Azure Production Deployment Report

## Purpose And Scope

This document explains how to deploy the `testapi1` capstone backend to a production-oriented Azure environment. It is written as a target-state deployment plan for the finished production system, not as a claim that every ML platform artifact already exists in the current repository.

The current repository already contains the core application that must be deployed:

- An ASP.NET Core `.NET 8` API
- PostgreSQL-backed progression/session persistence
- Redis-backed caching with an in-memory fallback
- Qdrant as the vector store
- ONNX embedding models copied into the published build
- A configurable local or remote LLM integration

For the production design in this report, the AI platform is expanded to include:

- Azure Machine Learning for GPU inference
- Azure Machine Learning pipeline jobs for fine-tuning
- Azure Blob Storage / Data Lake Gen2 for training data and artifacts
- Azure ML registry-based model promotion from `dev` to `staging` to `prod`

Local verification in this workspace on April 6, 2026 established that the current repo is deployable as an application baseline:

- `dotnet test .\testapi1.Tests\testapi1.Tests.csproj -v minimal` passed with `34/34` tests
- `dotnet publish -c Release` succeeded

Related repo artifacts:

- [README.md](/C:/Users/ysharma1/source/repos/testapi1/README.md)
- [Program.cs](/C:/Users/ysharma1/source/repos/testapi1/Program.cs)
- [appsettings.Production.json](/C:/Users/ysharma1/source/repos/testapi1/appsettings.Production.json)
- [docs/architecture/azure-production-architecture.md](/C:/Users/ysharma1/source/repos/testapi1/docs/architecture/azure-production-architecture.md)
- [ml/pipelines/retrain.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/pipelines/retrain.yml)

## 1. Application Architecture

### 1.1 Application Purpose

`testapi1` is the backend for an interrogation-style gameplay experience. The API receives player actions and dialogue turns, updates progression state, retrieves context, classifies intent, and generates NPC responses. In production, it is responsible for serving the Unity client while also participating in a larger ML lifecycle that retrains and promotes updated dialogue models.

### 1.2 Production Service Inventory

| Service | Product | Role In The System | Why This Choice |
| --- | --- | --- | --- |
| API host | Azure App Service | Runs the ASP.NET Core API and exposes the gameplay endpoints | Best fit for a `.NET 8` web API with lower operations overhead than AKS |
| Web deployment package | `dotnet publish -c Release` output | Published application artifact deployed to App Service | Matches the current repo and build tooling |
| Database | Azure Database for PostgreSQL Flexible Server | Stores authoritative progression, sessions, and runtime data | Managed backups, patching, TLS, and easy scaling |
| Cache | Azure Managed Redis | Stores distributed cache entries for intent and LLM-related caching | Better scale-out behavior than per-instance in-memory caching |
| Vector store | Qdrant Cloud | Stores semantic vectors and retrieval collections | The current code already targets Qdrant directly |
| Artifact storage | Azure Blob Storage / Data Lake Gen2 | Stores raw logs, curated datasets, evaluation sets, and model artifacts | Durable object storage for ML and operational assets |
| ML platform | Azure Machine Learning Workspace | Central control plane for training, experiments, deployments, and schedules | Native Azure integration for model lifecycle management |
| GPU training compute | Azure ML compute clusters | Runs fine-tuning and heavy evaluation jobs | Supports burst GPU workloads without always-on VM cost |
| Model registry | Azure ML registry | Versions models, components, and environments across environments | Clean promotion path from `dev` to `staging` to `prod` |
| GPU inference | Azure ML managed online endpoint | Hosts the approved dialogue model for real-time inference | Production-grade managed GPU serving |
| LLM compatibility layer | Azure Function or API Management-backed gateway | Exposes an OpenAI-compatible `/v1/chat/completions` route for the current API client | Preserves the app's existing `LlmService` contract while the model runs in Azure ML |
| Secrets | Azure Key Vault | Stores secrets, keys, connection strings, and endpoint credentials | Keeps secrets out of source control and App Service files |
| Observability | Application Insights, Azure Monitor, Log Analytics | Collects application logs, endpoint metrics, and alerting signals | Standard Azure monitoring stack for app plus ML workloads |

### 1.3 Online Request Flow

1. The Unity client sends requests to the ASP.NET Core API hosted on Azure App Service.
2. The API validates the request, reads or writes progression data in Azure Database for PostgreSQL, and uses Azure Managed Redis for cache-backed lookups.
3. The API queries Qdrant Cloud for semantic retrieval data when retrieval is required.
4. The API builds the final dialogue prompt and sends it to the LLM compatibility gateway.
5. The gateway forwards the request to the Azure ML managed online endpoint running the fine-tuned model on GPU compute.
6. The model response is returned to the API in an OpenAI-compatible format, then returned to the client.

### 1.4 Offline ML Flow

1. Conversation logs, QA transcripts, and authored examples land in Azure Blob Storage.
2. An Azure ML pipeline ingests those files and sanitizes them to remove PII and malformed records.
3. A curation step creates versioned `train`, `validation`, and `test` datasets.
4. A GPU fine-tuning step trains a new candidate model.
5. An evaluation step compares the candidate model to the current production baseline.
6. If quality and safety checks pass, the candidate model is registered in the Azure ML registry.
7. The model is deployed to staging first, manually approved, then promoted to production using blue/green or canary traffic shifting.

### 1.5 Why These Services Were Chosen

- **Azure App Service instead of AKS**: this project is a single backend API, not a platform that already needs cluster orchestration. App Service is easier to explain, cheaper to operate, and more realistic for a student capstone production plan.
- **PostgreSQL Flexible Server instead of a VM-hosted database**: the application already uses Npgsql and EF Core, so the Azure managed PostgreSQL service is a direct fit with fewer operational tasks.
- **Azure Managed Redis instead of in-memory only**: the code supports an in-memory fallback, but production scale-out requires a shared cache to avoid inconsistent results across instances.
- **Qdrant Cloud instead of replacing the vector layer**: this keeps the production plan aligned with the current code and avoids rewriting retrieval logic.
- **Azure ML instead of raw GPU VMs for the full AI lifecycle**: one platform can own fine-tuning jobs, experiment tracking, endpoint deployment, scheduling, and model registration.
- **A small compatibility gateway in front of Azure ML inference**: the current app expects an OpenAI-style `/v1/chat/completions` interface. A thin adapter avoids invasive application changes while still keeping Azure ML as the actual GPU inference host.

## 2. Environment Configuration

### 2.1 Prerequisites

The following tools, access, and accounts are required before deployment:

| Requirement | Why It Is Needed |
| --- | --- |
| Azure subscription | Required to create Azure resources |
| Microsoft Entra tenant access | Required for RBAC, managed identities, and admin sign-in |
| GPU quota in the target Azure region | Required for Azure ML GPU training and inference |
| .NET 8 SDK | Needed to restore, test, and publish the API |
| Python 3.10+ | Needed for the current local seed scripts and ML utility tooling |
| Azure CLI | Used to provision Azure resources |
| Azure ML CLI v2 extension | Used to create AML workspaces, compute, endpoints, and schedules |
| Git | Required to clone and version the repo |
| Docker Desktop | Still useful locally for reproducing Postgres/Redis/Qdrant dev dependencies |
| Qdrant Cloud account | Required if using managed Qdrant instead of self-hosted Qdrant on Azure |

Recommended bootstrap commands:

```powershell
az login
az account set --subscription "<subscription-name-or-id>"
az extension add -n ml
dotnet --info
python --version
```

### 2.2 Configuration Files

| File | Purpose |
| --- | --- |
| [appsettings.json](/C:/Users/ysharma1/source/repos/testapi1/appsettings.json) | Current default app settings |
| [appsettings.Development.json](/C:/Users/ysharma1/source/repos/testapi1/appsettings.Development.json) | Local development overrides |
| [appsettings.Production.json](/C:/Users/ysharma1/source/repos/testapi1/appsettings.Production.json) | Production-oriented settings template added for this deployment plan |
| [.env.example](/C:/Users/ysharma1/source/repos/testapi1/.env.example) | Local environment-variable template |
| [ml/pipelines/retrain.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/pipelines/retrain.yml) | Starter Azure ML retraining pipeline template |
| [ml/pipelines/retrain-schedule.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/pipelines/retrain-schedule.yml) | Weekly Azure ML retraining schedule template |
| [ml/endpoints/prod-endpoint.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/endpoints/prod-endpoint.yml) | Production online endpoint definition |
| [ml/endpoints/prod-deployment.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/endpoints/prod-deployment.yml) | Production online deployment definition |
| [ml/components/README.md](/C:/Users/ysharma1/source/repos/testapi1/ml/components/README.md) | Overview of the planned ML pipeline components |

### 2.3 Production Environment Variables

The API should keep secrets in Key Vault and surface them to App Service as application settings or Key Vault references.

| Setting | Example Production Source | Notes |
| --- | --- | --- |
| `CONNECTIONSTRINGS__POSTGRES` | Key Vault secret | Points to Azure Database for PostgreSQL Flexible Server |
| `CONNECTIONSTRINGS__REDIS` | Key Vault secret | Points to Azure Managed Redis |
| `QDRANT__BASEURL` | App Service setting | Points to Qdrant Cloud cluster URL |
| `QDRANT__COLLECTIONNAME` | App Service setting | Example: `intent-seed-prod` |
| `QDRANT__APIKEY` | Key Vault secret | Qdrant Cloud API key |
| `VECTORSTORE__PROVIDER` | App Service setting | Set to `Qdrant` |
| `LLM__LOCAL__ENABLED` | App Service setting | Set to `false` in production |
| `LLM__REMOTE__ENABLED` | App Service setting | Set to `true` in production |
| `LLM__REMOTE__BASEURL` | App Service setting | Points to the compatibility gateway base URL |
| `LLM__REMOTE__MODEL` | App Service setting | Example: `dylan-dialogue-prod` |
| `LLM__REMOTE__APIKEY` | Key Vault secret | Gateway or endpoint credential if required |
| `REMOTECONNECTIVITY__TIMEOUTMS` | App Service setting | Timeout for dependency checks |

### 2.4 Development Vs Production

| Area | Development | Production |
| --- | --- | --- |
| API host | `dotnet run` on localhost | Azure App Service |
| Postgres | Docker container on port `55432` | Azure Database for PostgreSQL Flexible Server |
| Redis | Docker container on port `6379` | Azure Managed Redis |
| Qdrant | Local Docker container | Qdrant Cloud |
| LLM host | Local Ollama or other localhost endpoint | Azure ML managed online endpoint on GPU behind a compatibility gateway |
| Secrets | `.env` file | Azure Key Vault + App Service settings |
| Logs | Local rolling file logs | App Service logs + Application Insights + Log Analytics |
| Model lifecycle | Manual local testing | Azure ML pipeline, registry, staging, and controlled promotion |

## 3. Deployment Process

This section describes a production deployment sequence that someone else could follow.

### 3.1 Define Naming Conventions

Use one consistent prefix. The examples below use `testapi1-prod`.

```powershell
$Location = "eastus"
$ResourceGroup = "rg-testapi1-prod"
$AppServicePlan = "asp-testapi1-prod"
$WebApp = "app-testapi1-prod"
$PostgresServer = "pg-testapi1-prod"
$RedisName = "redis-testapi1-prod"
$StorageAccount = "sttestapi1prod"
$KeyVault = "kv-testapi1-prod"
$AppInsights = "appi-testapi1-prod"
$Workspace = "aml-testapi1-prod"
$Registry = "mlr-testapi1-prod"
```

### 3.2 Create The Resource Group And Observability Resources

```powershell
az group create --name $ResourceGroup --location $Location

az monitor app-insights component create `
  --app $AppInsights `
  --location $Location `
  --resource-group $ResourceGroup `
  --application-type web

az keyvault create `
  --name $KeyVault `
  --resource-group $ResourceGroup `
  --location $Location

az storage account create `
  --name $StorageAccount `
  --resource-group $ResourceGroup `
  --location $Location `
  --sku Standard_LRS `
  --kind StorageV2 `
  --hierarchical-namespace true
```

### 3.3 Provision Database, Cache, And Vector Infrastructure

Create the Azure-managed data services first so their connection strings can be stored in Key Vault before the API is deployed.

```powershell
az postgres flexible-server create `
  --name $PostgresServer `
  --resource-group $ResourceGroup `
  --location $Location `
  --sku-name Standard_D2ds_v5 `
  --tier GeneralPurpose `
  --storage-size 128 `
  --version 16 `
  --admin-user pgadmin `
  --admin-password "<strong-password>"

az redis create `
  --name $RedisName `
  --resource-group $ResourceGroup `
  --location $Location `
  --sku Basic `
  --vm-size c1
```

For Qdrant Cloud:

1. Create a production cluster in the Qdrant Cloud portal.
2. Copy the cluster URL and API key.
3. Store both in Key Vault.
4. Set the intended production collection name, for example `intent-seed-prod`.

### 3.4 Provision Azure ML

```powershell
az ml workspace create `
  --name $Workspace `
  --resource-group $ResourceGroup `
  --location $Location

az ml compute create `
  --name gpu-train-cluster `
  --resource-group $ResourceGroup `
  --workspace-name $Workspace `
  --type amlcompute `
  --min-instances 0 `
  --max-instances 2 `
  --size Standard_NC4as_T4_v3

az ml compute create `
  --name cpu-orchestrator `
  --resource-group $ResourceGroup `
  --workspace-name $Workspace `
  --type amlcompute `
  --min-instances 0 `
  --max-instances 2 `
  --size Standard_DS3_v2
```

Create an Azure ML registry for cross-environment asset promotion if your subscription and region support it. If a shared registry is not available, keep model versioning inside the workspace and document that compromise.

### 3.5 Create The GPU Inference Endpoint

The repo includes starter endpoint files under [`ml/endpoints`](/C:/Users/ysharma1/source/repos/testapi1/ml/endpoints/README.md). Review the endpoint name, model version, compute SKU, and scoring configuration first.

```powershell
az ml online-endpoint create `
  --resource-group $ResourceGroup `
  --workspace-name $Workspace `
  --file .\ml\endpoints\staging-endpoint.yml

az ml online-deployment create `
  --resource-group $ResourceGroup `
  --workspace-name $Workspace `
  --file .\ml\endpoints\staging-deployment.yml `
  --all-traffic
```

After staging validation is complete, create the production endpoint and deployment:

```powershell
az ml online-endpoint create `
  --resource-group $ResourceGroup `
  --workspace-name $Workspace `
  --file .\ml\endpoints\prod-endpoint.yml

az ml online-deployment create `
  --resource-group $ResourceGroup `
  --workspace-name $Workspace `
  --file .\ml\endpoints\prod-deployment.yml `
  --all-traffic
```

### 3.6 Create The App Service Host

```powershell
az appservice plan create `
  --name $AppServicePlan `
  --resource-group $ResourceGroup `
  --location $Location `
  --sku P1v3 `
  --is-linux

az webapp create `
  --name $WebApp `
  --resource-group $ResourceGroup `
  --plan $AppServicePlan `
  --runtime "DOTNETCORE:8.0"
```

Enable a system-assigned managed identity so the app can read secrets from Key Vault:

```powershell
az webapp identity assign `
  --name $WebApp `
  --resource-group $ResourceGroup
```

### 3.7 Store Secrets And Configure App Settings

Store the connection strings, API keys, and endpoint credentials in Key Vault. Then configure App Service settings to read them.

Example values to configure:

```text
ASPNETCORE_ENVIRONMENT=Production
VECTORSTORE__PROVIDER=Qdrant
QDRANT__COLLECTIONNAME=intent-seed-prod
LLM__LOCAL__ENABLED=false
LLM__REMOTE__ENABLED=true
LLM__REMOTE__BASEURL=https://llm-gateway.contoso.com/v1
LLM__REMOTE__MODEL=dylan-dialogue-prod
REMOTECONNECTIVITY__TIMEOUTMS=3000
```

The following should come from Key Vault references instead of plain text:

- `CONNECTIONSTRINGS__POSTGRES`
- `CONNECTIONSTRINGS__REDIS`
- `QDRANT__APIKEY`
- `LLM__REMOTE__APIKEY`

### 3.8 Build And Deploy The API

Build a release artifact:

```powershell
dotnet restore
dotnet test .\testapi1.Tests\testapi1.Tests.csproj -v minimal
dotnet publish -c Release
```

Deploy the published output:

```powershell
Compress-Archive `
  -Path .\bin\Release\net8.0\publish\* `
  -DestinationPath .\publish.zip `
  -Force

az webapp deploy `
  --resource-group $ResourceGroup `
  --name $WebApp `
  --src-path .\publish.zip `
  --type zip
```

### 3.9 Run Database Migrations And Seed Data

Apply the EF Core migrations against the production PostgreSQL instance after deployment credentials are confirmed:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update
```

Seed the authoritative application data and Qdrant collection:

```powershell
python scripts/seed_postgres.py --force-reset
python scripts/seed_qdrant_intents.py
```

For a true production rollout, seed from an approved production dataset rather than the destructive developer reset dataset. The commands above are included because they are the repo's current seeding mechanism.

### 3.10 Register The Fine-Tuning Pipeline

The repo includes a starter AML pipeline and weekly schedule template:

- [ml/pipelines/retrain.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/pipelines/retrain.yml)
- [ml/pipelines/retrain-schedule.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/pipelines/retrain-schedule.yml)

Create the pipeline assets and then register the weekly schedule:

```powershell
az ml job create `
  --resource-group $ResourceGroup `
  --workspace-name $Workspace `
  --file .\ml\pipelines\retrain.yml

az ml schedule create `
  --resource-group $ResourceGroup `
  --workspace-name $Workspace `
  --file .\ml\pipelines\retrain-schedule.yml
```

The recommended process is:

1. Run the pipeline manually first.
2. Review the outputs and metrics.
3. Validate staging deployment behavior.
4. Only then turn on the recurring weekly schedule.

### 3.11 Verify The Deployment

Deployment is successful only when all of the following checks pass:

```powershell
curl https://<app-service-host>/api/v1/infra/dependencies
```

Expected verification checklist:

- App Service returns `200` for healthy probes and starts without configuration errors
- `/api/v1/infra/dependencies` reports healthy Postgres, Redis, Qdrant, and LLM connectivity
- Swagger or API endpoints respond successfully
- PostgreSQL contains the migrated schema
- Qdrant contains the intended collection and vectors
- Azure ML staging and production endpoints respond within the expected latency budget
- The gameplay flow works end-to-end:
  - `POST /api/v1/progression/start`
  - `POST /api/v1/progression/clues/click`
  - `POST /api/v1/progression/turn`

## 4. Security Implementation

### 4.1 Authentication And Authorization

The current prototype code does not yet include full application-level authentication middleware. For production, the target system should use:

- Microsoft Entra ID for administrative and service access
- App Service Authentication / Authorization at the platform edge or JWT validation inside the app
- Managed identities for service-to-service access from the API to Key Vault and other Azure resources

Recommended production policy:

- Unity clients authenticate to the API using application-issued or Entra-backed tokens
- Admin and ML operator access is restricted through Entra group membership
- Azure ML, Key Vault, storage, and monitoring permissions are assigned through RBAC, not shared credentials

### 4.2 Security Configurations

Planned production controls:

- TLS for all public endpoints
- Private endpoints or restricted network rules for PostgreSQL, Redis, storage, and AML where supported
- Key Vault for secrets instead of `.env` files
- Locked-down production CORS instead of `AllowAnyOrigin`
- Separate resource groups or subscriptions for `dev`, `staging`, and `prod`
- Principle of least privilege for App Service managed identity, ML jobs, and deployment operators

### 4.3 Secret Management

Sensitive values that must not be stored in source control:

- PostgreSQL connection strings
- Redis connection strings
- Qdrant API keys
- AML endpoint or compatibility-gateway credentials
- Storage account access credentials if not using managed identity

Secret management approach:

1. Store secrets in Azure Key Vault.
2. Grant the App Service managed identity read access only to the needed secrets.
3. Reference those secrets in App Service configuration.
4. Keep non-secret defaults in `appsettings.Production.json`.

### 4.4 Training Data Governance

Because the production system includes fine-tuning, security also applies to ML data handling:

- Raw transcripts must be sanitized before joining the training set
- PII removal must happen before data becomes a reusable AML asset
- Each dataset version must be traceable to its source batch and sanitization run
- Promotion to production requires both evaluation evidence and human approval
- The previous production model version remains available for rollback

## 5. Monitoring And Maintenance

### 5.1 How To Check If The Application Is Running Correctly

Primary runtime checks:

- Azure App Service health status
- `GET /api/v1/infra/dependencies`
- Application Insights availability and error rates
- PostgreSQL connectivity
- Redis connectivity
- Qdrant health and collection reachability
- AML endpoint health, latency, and error rate

### 5.2 How To View Logs

| Area | Logging Method |
| --- | --- |
| API application | App Service log streaming, file logs, and Application Insights |
| Dependency issues | `/api/v1/infra/dependencies` plus application logs |
| Database | PostgreSQL server logs and connection diagnostics |
| Cache | Redis metrics and platform diagnostics |
| ML training | Azure ML job logs, metrics, and artifacts |
| ML inference | Azure ML online endpoint logs, request latency, and scale metrics |
| Promotion history | Azure ML registry history and deployment revision records |

### 5.3 How To Update The Application

Recommended application update flow:

1. Make the code change in Git.
2. Run tests locally or in CI.
3. Build a new release with `dotnet publish -c Release`.
4. Deploy the updated package to the staging slot or staging App Service.
5. Run smoke tests.
6. Swap staging into production or deploy to production after approval.

Recommended model update flow:

1. New training data lands in storage.
2. The AML retraining pipeline runs.
3. Candidate metrics are compared with the current production baseline.
4. The approved model is registered.
5. The candidate is deployed to the staging endpoint.
6. If staging smoke tests pass, traffic is shifted to the new production deployment.
7. If problems appear, shift traffic back to the previous deployment immediately.

### 5.4 Basic Troubleshooting Guide

| Problem | Likely Cause | Resolution |
| --- | --- | --- |
| App Service starts but dependency probe fails | Missing or invalid connection string or secret reference | Verify App Service settings and Key Vault references |
| PostgreSQL migration fails | Database unreachable or wrong credentials | Test connection string and firewall/private endpoint rules |
| Redis health check fails | Wrong endpoint, auth failure, or network rule mismatch | Confirm Redis host, TLS settings, and network access |
| Qdrant retrieval fails | Bad Qdrant URL, collection name, or API key | Re-check `QDRANT__BASEURL`, `QDRANT__COLLECTIONNAME`, and API key |
| LLM calls fail with path or schema errors | Compatibility gateway not aligned with the API contract | Verify gateway route is `/v1/chat/completions` and response matches current `LlmService` expectations |
| AML deployment is unhealthy | Wrong model artifact, broken scoring script, or insufficient GPU capacity | Review Azure ML endpoint logs and rollout the previous deployment |
| Fine-tuning schedule fails | Missing dataset asset, quota shortage, or component misconfiguration | Re-run the pipeline manually and inspect the failing step in Azure ML |
| New model performs worse | Candidate failed evaluation or prompt regression | Keep traffic on the previous version and reject production promotion |

## Supporting Production Artifacts Added To This Repo

The following files were added as starter templates to support this deployment plan:

- [appsettings.Production.json](/C:/Users/ysharma1/source/repos/testapi1/appsettings.Production.json)
- [docs/architecture/azure-production-architecture.md](/C:/Users/ysharma1/source/repos/testapi1/docs/architecture/azure-production-architecture.md)
- [ml/README.md](/C:/Users/ysharma1/source/repos/testapi1/ml/README.md)
- [ml/components/README.md](/C:/Users/ysharma1/source/repos/testapi1/ml/components/README.md)
- [ml/pipelines/retrain.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/pipelines/retrain.yml)
- [ml/endpoints/README.md](/C:/Users/ysharma1/source/repos/testapi1/ml/endpoints/README.md)

These files are intentionally honest about their status:

- The application is real and buildable now.
- The production ML platform is described as the intended end state.
- The AML pipeline and endpoint files are starter templates that show what must exist for production, even if they are not yet wired into the current local developer loop.

## References

- Azure App Service overview: [learn.microsoft.com/en-us/azure/app-service/overview](https://learn.microsoft.com/en-us/azure/app-service/overview)
- Azure Database for PostgreSQL Flexible Server overview: [learn.microsoft.com/en-us/azure/postgresql/flexible-server/overview](https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/overview)
- Azure Machine Learning overview: [learn.microsoft.com/en-us/azure/machine-learning/overview-what-is-azure-machine-learning](https://learn.microsoft.com/en-us/azure/machine-learning/overview-what-is-azure-machine-learning)
- Azure ML endpoints: [learn.microsoft.com/en-us/azure/machine-learning/concept-endpoints](https://learn.microsoft.com/en-us/azure/machine-learning/concept-endpoints)
- Azure ML compute management: [learn.microsoft.com/en-us/azure/machine-learning/how-to-create-attach-compute-studio?view=azureml-api-2](https://learn.microsoft.com/en-us/azure/machine-learning/how-to-create-attach-compute-studio?view=azureml-api-2)
- Azure ML MLflow configuration: [learn.microsoft.com/en-us/azure/machine-learning/how-to-use-mlflow-configure-tracking?view=azureml-api-2](https://learn.microsoft.com/en-us/azure/machine-learning/how-to-use-mlflow-configure-tracking?view=azureml-api-2)
- Azure AI Foundry fine-tuning overview: [learn.microsoft.com/azure/ai-foundry/concepts/fine-tuning-overview](https://learn.microsoft.com/azure/ai-foundry/concepts/fine-tuning-overview?azure-portal=true)
- Qdrant overview: [qdrant.tech/documentation/overview](https://qdrant.tech/documentation/overview/)
