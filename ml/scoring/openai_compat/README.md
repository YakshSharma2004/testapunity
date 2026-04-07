# OpenAI-Compatible Scoring Shim

This folder contains the starter scoring script for the planned Azure ML GPU deployment.

The goal of this shim is to make the production model return the same high-level response shape that the current `Services/LlmService.cs` expects:

- OpenAI-style `choices`
- `message.content`
- `usage.total_tokens`
- `finish_reason`

In a full production implementation, this scoring package should:

1. Load the approved fine-tuned model during `init()`.
2. Translate the incoming request into the model's prompt format.
3. Run inference on the GPU-backed Azure ML deployment.
4. Return an OpenAI-compatible response body to the compatibility gateway.
5. Emit structured logs for latency, tokens, model version, and fallback conditions.
