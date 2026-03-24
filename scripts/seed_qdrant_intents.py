#!/usr/bin/env python3
"""
Seed intent vectors into Qdrant using the same tokenizer + ONNX embedding path as the ASP.NET app.

Requirements:
  pip install onnxruntime tokenizers

Usage examples:
  python scripts/seed_qdrant_intents.py
  python scripts/seed_qdrant_intents.py --model multiqa --dry-run
  python scripts/seed_qdrant_intents.py --qdrant-url http://localhost:6333 --collection intent-seed-poc
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Any
from urllib import error, parse, request

import numpy as np


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_ENV_FILE = REPO_ROOT / ".env"
SEED_VERSION = "intent-seed-v1"
SEED_NAMESPACE = uuid.UUID("7b50d7d8-a0b4-4f39-b706-ae6f61b4e4c1")

DEFAULT_INTENT_SEEDS: dict[str, list[str]] = {
    "ASK_OPEN_QUESTION": [
        "Tell me what happened tonight.",
        "Start from the beginning and walk me through it.",
        "What do you remember about Elsa that evening?",
        "Help me understand your side of this.",
    ],
    "ASK_TIMELINE": [
        "When did you arrive and when did you leave?",
        "Give me your exact timeline from start to finish.",
        "What time were you in Elsa's office area?",
        "Walk me through the sequence of events.",
    ],
    "EMPATHY": [
        "I know this situation is heavy, but help me understand it.",
        "If something went wrong, this is the time to explain it.",
        "I'm giving you a chance to clear this up.",
        "You can be honest with me without me twisting your words.",
    ],
    "PRESENT_EVIDENCE": [
        "We found the payroll discrepancy report tied to your department.",
        "The email draft points back to a conflict with you.",
        "The access log puts you there later than you claimed.",
        "We opened the suitcase and the contents are a problem for you.",
    ],
    "CONTRADICTION": [
        "That does not line up with what you said earlier.",
        "Your story keeps changing when I bring up the timeline.",
        "You said you left, but the evidence says otherwise.",
        "That explanation contradicts the entry log.",
    ],
    "SILENCE": [
        "...",
        "I'm waiting.",
        "Take your time. The silence is yours.",
        "Go ahead. Explain the gap.",
    ],
    "INTIMIDATE": [
        "Stop dodging and answer the question directly.",
        "This is your chance before the evidence closes in on you.",
        "Do not make this worse by lying again.",
        "You are running out of room to hide behind excuses.",
    ],
    "CLOSE_INTERROGATION": [
        "We're done here for now.",
        "I'm ending this interview.",
        "That will be all for tonight.",
        "The interrogation is over.",
    ],
}


@dataclass(frozen=True)
class EmbeddingModelConfig:
    model_name: str
    model_path: Path
    tokenizer_path: Path
    max_len: int


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Seed intent vectors into Qdrant.")
    parser.add_argument("--model", choices=["mpnet", "multiqa"], help="Embedding model to use.")
    parser.add_argument("--qdrant-url", help="Qdrant base URL.")
    parser.add_argument("--collection", help="Qdrant collection name.")
    parser.add_argument("--api-key", help="Qdrant API key.")
    parser.add_argument("--env-file", default=str(DEFAULT_ENV_FILE), help=f"Path to .env file (default: {DEFAULT_ENV_FILE}).")
    parser.add_argument("--dry-run", action="store_true", help="Prepare embeddings and print plan without writing to Qdrant.")
    return parser.parse_args()


def read_env_file(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    if not path.exists():
        return values

    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        key = key.strip()
        value = value.strip()
        if (value.startswith('"') and value.endswith('"')) or (value.startswith("'") and value.endswith("'")):
            value = value[1:-1]
        values[key] = value
    return values


def read_json_file(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))


def deep_get(data: dict[str, Any], *keys: str, default: Any = None) -> Any:
    current: Any = data
    for key in keys:
        if not isinstance(current, dict) or key not in current:
            return default
        current = current[key]
    return current


def normalize_model_name(value: str | None) -> str:
    if not value:
        return "mpnet"
    lowered = value.strip().lower()
    return "mpnet" if lowered == "current" else lowered


def resolve_model_config(args: argparse.Namespace, env_values: dict[str, str]) -> EmbeddingModelConfig:
    appsettings = read_json_file(REPO_ROOT / "appsettings.json")
    development = read_json_file(REPO_ROOT / "appsettings.Development.json")

    configured_model = (
        args.model
        or os.getenv("EMBEDDINGS__MODEL")
        or env_values.get("EMBEDDINGS__MODEL")
        or deep_get(development, "Embeddings", "Model")
        or deep_get(appsettings, "Embeddings", "Model")
        or "mpnet"
    )
    model_name = normalize_model_name(configured_model)

    model_settings = (
        deep_get(development, "Embeddings", "Models", model_name)
        or deep_get(appsettings, "Embeddings", "Models", model_name)
    )
    if not isinstance(model_settings, dict):
        raise ValueError(f"Could not resolve embedding settings for model '{model_name}'.")

    model_path = resolve_repo_path(str(model_settings.get("ModelPath", "")))
    tokenizer_path = resolve_repo_path(str(model_settings.get("TokenizerPath", "")))
    max_len = int(model_settings.get("MaxLen", 384))

    return EmbeddingModelConfig(
        model_name=model_name,
        model_path=model_path,
        tokenizer_path=tokenizer_path,
        max_len=max_len,
    )


def resolve_repo_path(configured_path: str) -> Path:
    if not configured_path:
        raise ValueError("ModelPath/TokenizerPath are required.")
    path = Path(configured_path)
    return path if path.is_absolute() else (REPO_ROOT / path).resolve()


def resolve_qdrant_settings(args: argparse.Namespace, env_values: dict[str, str]) -> tuple[str, str, str]:
    appsettings = read_json_file(REPO_ROOT / "appsettings.json")
    development = read_json_file(REPO_ROOT / "appsettings.Development.json")

    qdrant_url = (
        args.qdrant_url
        or os.getenv("QDRANT__BASEURL")
        or env_values.get("QDRANT__BASEURL")
        or deep_get(development, "Qdrant", "BaseUrl")
        or deep_get(appsettings, "Qdrant", "BaseUrl")
        or "http://localhost:6333"
    )
    collection = (
        args.collection
        or os.getenv("QDRANT__COLLECTIONNAME")
        or env_values.get("QDRANT__COLLECTIONNAME")
        or deep_get(development, "Qdrant", "CollectionName")
        or deep_get(appsettings, "Qdrant", "CollectionName")
        or "intent-seed-poc"
    )
    api_key = args.api_key or os.getenv("QDRANT__APIKEY") or env_values.get("QDRANT__APIKEY", "")

    return qdrant_url.strip().rstrip("/"), collection.strip(), api_key.strip()


def require_embedding_runtime():
    try:
        import onnxruntime as ort  # type: ignore
    except ImportError as exc:  # pragma: no cover
        raise RuntimeError("onnxruntime is not installed. Install it with: pip install onnxruntime") from exc

    try:
        from tokenizers import Tokenizer  # type: ignore
    except ImportError as exc:  # pragma: no cover
        raise RuntimeError("tokenizers is not installed. Install it with: pip install tokenizers") from exc

    return ort, Tokenizer


class OnnxSentenceEmbedder:
    def __init__(self, config: EmbeddingModelConfig):
        ort, tokenizer_type = require_embedding_runtime()

        if not config.model_path.exists():
            raise FileNotFoundError(f"Embedding model file not found: {config.model_path}")
        if not config.tokenizer_path.exists():
            raise FileNotFoundError(f"Tokenizer file not found: {config.tokenizer_path}")

        self.model_name = config.model_name
        self.max_len = config.max_len
        self.tokenizer = tokenizer_type.from_file(str(config.tokenizer_path))
        self.session = ort.InferenceSession(str(config.model_path))
        input_names = [item.name for item in self.session.get_inputs()]
        self.input_ids_name = find_input_name(input_names, "input_ids", "input_ids:0")
        self.attention_mask_name = find_input_name(input_names, "attention_mask", "attention_mask:0", "mask")
        self.token_type_ids_name = find_input_name(input_names, "token_type_ids", "token_type_ids:0", "segment_ids")

        if not self.input_ids_name:
            raise ValueError(f"Model '{self.model_name}' does not expose input_ids.")
        if not self.attention_mask_name:
            raise ValueError(f"Model '{self.model_name}' does not expose attention_mask.")

    def embed(self, text: str) -> np.ndarray:
        encoded = self.tokenizer.encode(text or "")
        token_ids = list(encoded.ids)[: self.max_len]

        input_ids = np.zeros((1, self.max_len), dtype=np.int64)
        attention_mask = np.zeros((1, self.max_len), dtype=np.int64)
        token_type_ids = np.zeros((1, self.max_len), dtype=np.int64)

        if token_ids:
            input_ids[0, : len(token_ids)] = np.asarray(token_ids, dtype=np.int64)
            attention_mask[0, : len(token_ids)] = 1

        inputs: dict[str, np.ndarray] = {
            self.input_ids_name: input_ids,
            self.attention_mask_name: attention_mask,
        }
        if self.token_type_ids_name:
            inputs[self.token_type_ids_name] = token_type_ids

        outputs = self.session.run(None, inputs)
        tensor = next((np.asarray(output) for output in outputs if np.issubdtype(np.asarray(output).dtype, np.floating)), None)
        if tensor is None:
            raise RuntimeError(f"Model '{self.model_name}' did not return a float tensor output.")

        return extract_embedding(tensor, attention_mask[0])


def find_input_name(keys: list[str], *candidates: str) -> str | None:
    lowered = {key.lower(): key for key in keys}
    for candidate in candidates:
        if candidate.lower() in lowered:
            return lowered[candidate.lower()]

    for key in keys:
        key_lower = key.lower()
        for candidate in candidates:
            if candidate.lower() in key_lower:
                return key
    return None


def extract_embedding(output: np.ndarray, attention_mask: np.ndarray) -> np.ndarray:
    if output.ndim == 2 and output.shape[0] == 1:
        embedding = output[0].astype(np.float32, copy=True)
        return l2_normalize(embedding)

    if output.ndim == 3 and output.shape[0] == 1:
        sequence = output[0].astype(np.float32, copy=False)
        mask = attention_mask[: sequence.shape[0]].astype(np.float32)
        token_count = float(mask.sum())

        if token_count <= 0.0:
            pooled = np.zeros(sequence.shape[1], dtype=np.float32)
        else:
            pooled = (sequence * mask[:, None]).sum(axis=0) / token_count

        return l2_normalize(pooled.astype(np.float32, copy=False))

    raise RuntimeError(f"Unsupported embedding tensor shape: {list(output.shape)}")


def l2_normalize(vector: np.ndarray) -> np.ndarray:
    norm = float(np.linalg.norm(vector))
    if norm <= 0.0:
        return vector.astype(np.float32, copy=False)
    return (vector / norm).astype(np.float32, copy=False)


def deterministic_point_id(intent_id: str, text: str) -> str:
    raw = f"{SEED_VERSION}:{intent_id}:{text}"
    return str(uuid.uuid5(SEED_NAMESPACE, raw))


def build_points(embedder: OnnxSentenceEmbedder) -> tuple[list[dict[str, Any]], int]:
    points: list[dict[str, Any]] = []
    dimension = 0

    for intent_id, examples in DEFAULT_INTENT_SEEDS.items():
        for example in examples:
            embedding = embedder.embed(example)
            if dimension == 0:
                dimension = int(embedding.shape[0])

            points.append(
                {
                    "id": deterministic_point_id(intent_id, example),
                    "vector": embedding.tolist(),
                    "payload": {
                        "intentId": intent_id,
                        "text": example,
                        "embeddingModel": embedder.model_name,
                        "seedVersion": SEED_VERSION,
                    },
                }
            )

    if dimension <= 0:
        raise RuntimeError("No seed points were generated.")

    return points, dimension


def qdrant_request(
    method: str,
    url: str,
    api_key: str,
    payload: dict[str, Any] | None = None,
    allow_not_found: bool = False,
) -> tuple[int, dict[str, Any] | None]:
    data = None if payload is None else json.dumps(payload).encode("utf-8")
    headers = {"Content-Type": "application/json"} if payload is not None else {}
    if api_key:
        headers["api-key"] = api_key

    req = request.Request(url, data=data, headers=headers, method=method)
    try:
        with request.urlopen(req) as response:
            body = response.read().decode("utf-8")
            return response.status, json.loads(body) if body else None
    except error.HTTPError as exc:
        if allow_not_found and exc.code == 404:
            return 404, None
        body = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"Qdrant request failed: {method} {url} -> {exc.code}. Body: {body}") from exc


def ensure_collection(qdrant_url: str, collection: str, api_key: str, dimension: int) -> None:
    collection_url = f"{qdrant_url}/collections/{parse.quote(collection, safe='')}"
    status, existing = qdrant_request("GET", collection_url, api_key, allow_not_found=True)

    if status == 404:
        qdrant_request(
            "PUT",
            collection_url,
            api_key,
            payload={"vectors": {"size": dimension, "distance": "Cosine"}},
        )
        return

    configured_size = deep_get(existing or {}, "result", "config", "params", "vectors", "size")
    if configured_size is not None and int(configured_size) != dimension:
        raise RuntimeError(
            f"Existing collection '{collection}' has vector size {configured_size}, but embeddings are size {dimension}."
        )


def upsert_points(qdrant_url: str, collection: str, api_key: str, points: list[dict[str, Any]]) -> None:
    upsert_url = f"{qdrant_url}/collections/{parse.quote(collection, safe='')}/points?wait=true"
    qdrant_request("PUT", upsert_url, api_key, payload={"points": points})


def search_points(qdrant_url: str, collection: str, api_key: str, vector: list[float], limit: int = 3) -> dict[str, Any]:
    search_url = f"{qdrant_url}/collections/{parse.quote(collection, safe='')}/points/search"
    _, payload = qdrant_request(
        "POST",
        search_url,
        api_key,
        payload={"vector": vector, "limit": limit, "with_payload": True},
    )
    return payload or {}


def smoke_check(embedder: OnnxSentenceEmbedder, qdrant_url: str, collection: str, api_key: str) -> None:
    samples = [
        ("When did you leave the building?", "ASK_TIMELINE"),
        ("The payroll report points back to you.", "PRESENT_EVIDENCE"),
        ("That story contradicts the access log.", "CONTRADICTION"),
    ]

    print("Verification queries:")
    for query, expected in samples:
        embedding = embedder.embed(query).tolist()
        payload = search_points(qdrant_url, collection, api_key, embedding, limit=3)
        results = payload.get("result") or []
        top = results[0] if results else {}
        top_payload = top.get("payload") or {}
        top_intent = top_payload.get("intentId", "<none>")
        top_score = top.get("score", 0.0)
        verdict = "OK" if top_intent == expected else "CHECK"
        print(f"  - {verdict}: '{query}' -> top={top_intent} score={top_score:.4f} expected={expected}")


def main() -> int:
    args = parse_args()
    env_path = Path(args.env_file).expanduser()
    if not env_path.is_absolute():
        env_path = (Path.cwd() / env_path).resolve()
    env_values = read_env_file(env_path)

    try:
        model_config = resolve_model_config(args, env_values)
        qdrant_url, collection, api_key = resolve_qdrant_settings(args, env_values)
        embedder = OnnxSentenceEmbedder(model_config)
        points, dimension = build_points(embedder)
    except Exception as exc:
        print(f"Configuration or embedding setup failed: {exc}", file=sys.stderr)
        return 2

    print(f"Embedding model: {model_config.model_name}")
    print(f"Model path: {model_config.model_path}")
    print(f"Tokenizer path: {model_config.tokenizer_path}")
    print(f"Max length: {model_config.max_len}")
    print(f"Qdrant URL: {qdrant_url}")
    print(f"Collection: {collection}")
    print(f"Seed version: {SEED_VERSION}")
    print(f"Seed points: {len(points)}")
    print(f"Vector dimension: {dimension}")

    if args.dry_run:
        print("Dry run complete. No Qdrant writes were performed.")
        return 0

    try:
        ensure_collection(qdrant_url, collection, api_key, dimension)
        upsert_points(qdrant_url, collection, api_key, points)
        smoke_check(embedder, qdrant_url, collection, api_key)
    except Exception as exc:
        print(f"Qdrant seed failed: {exc}", file=sys.stderr)
        return 1

    print("Intent vector seed completed successfully.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
