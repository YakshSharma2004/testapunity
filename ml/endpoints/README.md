# Azure ML Online Endpoint Templates

This folder contains starter templates for the staging and production GPU inference endpoints.

## Files

| File | Purpose |
| --- | --- |
| [staging-endpoint.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/endpoints/staging-endpoint.yml) | Staging endpoint definition |
| [staging-deployment.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/endpoints/staging-deployment.yml) | Staging deployment definition |
| [prod-endpoint.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/endpoints/prod-endpoint.yml) | Production endpoint definition |
| [prod-deployment.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/endpoints/prod-deployment.yml) | Production deployment definition |

## Contract Compatibility Note

The current `LlmService` in the application builds requests around an OpenAI-style `/v1/chat/completions` path and response body. Azure ML managed online endpoints expose a scoring endpoint, so the production deployment includes a compatibility layer.

Recommended implementation:

1. Host the actual model on Azure ML managed online endpoints.
2. Use the scoring shim under [ml/scoring/openai_compat/score.py](/C:/Users/ysharma1/source/repos/testapi1/ml/scoring/openai_compat/score.py) as the starting point for the AML deployment contract.
3. Front the AML endpoint with a lightweight Azure Function or API Management route that exposes `/v1/chat/completions` to the ASP.NET Core app.

That approach keeps the application unchanged while still making Azure ML the real GPU inference host.
