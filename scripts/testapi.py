#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import sys
from pathlib import Path

import requests
from openai import OpenAI


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

        # strip quotes
        if (value.startswith('"') and value.endswith('"')) or (value.startswith("'") and value.endswith("'")):
            value = value[1:-1]

        values[key] = value
    return values


def main() -> None:
    # default: .env next to this script
    #env_path = Path(__file__).resolve().parent / ".env"
    # OR: repo root .env (uncomment if you want)
    env_path = Path(__file__).resolve().parents[1]/".env"

    env = read_env_file(env_path)
    if "LLM__REMOTE__APIKEY" in env and "LLM__REMOTE__APIKEY" not in os.environ:
        os.environ["LLM__REMOTE__APIKEY"] = env["LLM__REMOTE__APIKEY"]

    api_key = os.getenv("LLM__REMOTE__APIKEY")
    if not api_key:
        print(f"ERROR: LLM__REMOTE__APIKEY not found. Put it in {env_path} or set it as an environment variable.")
        sys.exit(1)

    model = os.getenv("OPENAI_MODEL") or env.get("OPENAI_MODEL") or "o4-mini"

    # 1) Key validity check (/v1/me)
    r = requests.get(
        "https://api.openai.com/v1/me",
        headers={"Authorization": f"Bearer {api_key}"},
        timeout=20,
    )
    print("Auth check (/v1/me) status:", r.status_code)
    if r.status_code != 200:
        print("Body:", r.text)
        sys.exit(1)

    me = r.json()
    print("Authenticated user id:", me.get("id"), "email:", me.get("email"))

    # 2) Simple generation test
    client = OpenAI(api_key=api_key)
    resp = client.responses.create(
        model=model,
        input="Reply with exactly: OK",
    )

    print("\nResponses API worked. Output:")
    print(resp.output_text.strip() if hasattr(resp, "output_text") else json.dumps(resp.model_dump(), indent=2))


if __name__ == "__main__":
    main()