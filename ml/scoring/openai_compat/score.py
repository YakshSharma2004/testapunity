"""
Starter Azure ML scoring shim for the planned production LLM deployment.

This file is intentionally lightweight. It provides the contract shape that the
current ASP.NET Core backend expects, while making it obvious where the real
model-loading and inference logic should go in a future implementation.
"""

from __future__ import annotations

import json
from typing import Any


def init() -> None:
    """Load model artifacts here in the production implementation."""


def run(raw_data: str) -> dict[str, Any]:
    """
    Return an OpenAI-compatible chat completion response.

    Replace this placeholder implementation with real prompt handling and model
    inference once the GPU-hosted deployment package is finalized.
    """

    try:
        payload = json.loads(raw_data)
    except json.JSONDecodeError:
        payload = {}

    prompt_preview = ""
    if isinstance(payload, dict):
        messages = payload.get("messages") or []
        if isinstance(messages, list):
            prompt_preview = "\n".join(
                item.get("content", "")
                for item in messages
                if isinstance(item, dict)
            )[:200]

    return {
        "id": "starter-template",
        "object": "chat.completion",
        "model": "template-only",
        "choices": [
            {
                "index": 0,
                "message": {
                    "role": "assistant",
                    "content": (
                        "Replace ml/scoring/openai_compat/score.py with the "
                        "production Azure ML scoring implementation."
                    ),
                },
                "finish_reason": "stop",
            }
        ],
        "usage": {
            "total_tokens": 0
        },
        "prompt_preview": prompt_preview,
    }
