#!/usr/bin/env python3
"""
Reset + reseed app tables in Postgres with a deterministic developer dataset.

Usage examples:
  python scripts/seed_postgres.py --force-reset
  python scripts/seed_postgres.py --conn "Host=localhost;Port=55432;Database=testgame;Username=postgres;Password=secret" --force-reset
  python scripts/seed_postgres.py --dry-run
"""

from __future__ import annotations

import argparse
import json
import os
import sys
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from decimal import Decimal
from pathlib import Path
from typing import Any
from urllib.parse import urlparse


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_ENV_FILE = REPO_ROOT / ".env"
POSTGRES_CONN_KEY = "CONNECTIONSTRINGS__POSTGRES"

APP_TABLES_IN_RESET_ORDER = [
    "Interactions",
    "DialogueTemplates",
    "LoreChunks",
    "LoreDocs",
    "PlayerNpcStates",
    "ActionCatalog",
    "ProgressionSessions",
    "Players",
    "Npcs",
]

TRUNCATE_SQL = """
TRUNCATE TABLE
    "Interactions",
    "DialogueTemplates",
    "LoreChunks",
    "LoreDocs",
    "PlayerNpcStates",
    "ActionCatalog",
    "ProgressionSessions",
    "Players",
    "Npcs"
RESTART IDENTITY CASCADE;
""".strip()

IDENTITY_COLUMNS = [
    ("Players", "PlayerId"),
    ("Npcs", "NpcId"),
    ("ActionCatalog", "ActionId"),
    ("DialogueTemplates", "TemplateId"),
    ("LoreDocs", "DocId"),
    ("LoreChunks", "ChunkId"),
    ("Interactions", "InteractionId"),
]

SEED_TIMESTAMP_UTC = datetime(2026, 3, 23, 12, 0, 0, tzinfo=timezone.utc)


@dataclass(frozen=True)
class SeedTable:
    name: str
    insert_sql: str
    rows: list[tuple[Any, ...]]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Reset and reseed all app tables in Postgres (deterministic developer seed)."
    )
    parser.add_argument(
        "--conn",
        help="Full Postgres connection string (takes precedence over env and .env).",
    )
    parser.add_argument(
        "--env-file",
        default=str(DEFAULT_ENV_FILE),
        help=f"Path to .env file (default: {DEFAULT_ENV_FILE}).",
    )
    parser.add_argument(
        "--force-reset",
        action="store_true",
        help="Required for destructive TRUNCATE + reseed.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print SQL/data plan without executing or committing.",
    )
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
        if (value.startswith('"') and value.endswith('"')) or (
            value.startswith("'") and value.endswith("'")
        ):
            value = value[1:-1]
        values[key] = value
    return values


def resolve_connection_string(args: argparse.Namespace) -> tuple[str, str, Path]:
    if args.conn:
        return args.conn, "cli --conn", Path(args.env_file).expanduser().resolve()

    from_env = os.getenv(POSTGRES_CONN_KEY)
    if from_env:
        return from_env, f"env:{POSTGRES_CONN_KEY}", Path(args.env_file).expanduser().resolve()

    env_path = Path(args.env_file).expanduser()
    if not env_path.is_absolute():
        env_path = (Path.cwd() / env_path).resolve()

    env_values = read_env_file(env_path)
    from_file = env_values.get(POSTGRES_CONN_KEY)
    if from_file:
        return from_file, f"file:{env_path}", env_path

    raise ValueError(
        f"Could not resolve Postgres connection string. Tried --conn, env {POSTGRES_CONN_KEY}, and {env_path}."
    )


def parse_connection_for_psycopg2(raw_conn: str) -> tuple[dict[str, Any] | str, dict[str, str]]:
    raw_conn = raw_conn.strip()
    if raw_conn.lower().startswith(("postgres://", "postgresql://")):
        parsed = urlparse(raw_conn)
        target = {
            "host": parsed.hostname or "",
            "port": str(parsed.port or 5432),
            "db": (parsed.path or "").lstrip("/"),
            "user": parsed.username or "",
        }
        return raw_conn, target

    key_aliases = {
        "host": "host",
        "server": "host",
        "data source": "host",
        "port": "port",
        "database": "dbname",
        "initial catalog": "dbname",
        "username": "user",
        "user id": "user",
        "userid": "user",
        "user": "user",
        "uid": "user",
        "password": "password",
        "pwd": "password",
        "ssl mode": "sslmode",
        "sslmode": "sslmode",
        "timeout": "connect_timeout",
        "connect timeout": "connect_timeout",
    }

    parsed_kwargs: dict[str, Any] = {}
    for segment in raw_conn.split(";"):
        part = segment.strip()
        if not part or "=" not in part:
            continue
        raw_key, raw_value = part.split("=", 1)
        key = raw_key.strip().lower()
        value = raw_value.strip()
        mapped = key_aliases.get(key)
        if not mapped:
            continue
        if mapped in ("port", "connect_timeout"):
            try:
                parsed_kwargs[mapped] = int(value)
            except ValueError:
                continue
        else:
            parsed_kwargs[mapped] = value

    if "dbname" not in parsed_kwargs:
        raise ValueError("Connection string must include Database.")

    target = {
        "host": str(parsed_kwargs.get("host", "")),
        "port": str(parsed_kwargs.get("port", 5432)),
        "db": str(parsed_kwargs.get("dbname", "")),
        "user": str(parsed_kwargs.get("user", "")),
    }
    return parsed_kwargs, target


def build_seed_tables() -> list[SeedTable]:
    ts = SEED_TIMESTAMP_UTC
    session_id = "ps_11111111111111111111111111111111"

    action_rows = [
        (1, "ASK_OPEN_QUESTION", "ASK_OPEN_QUESTION", "Ask open-ended questions to gather broad context."),
        (2, "ASK_TIMELINE", "ASK_TIMELINE", "Probe chronology and sequence of events."),
        (3, "EMPATHY", "EMPATHY", "Use rapport-building language to lower resistance."),
        (4, "PRESENT_EVIDENCE", "PRESENT_EVIDENCE", "Present concrete evidence and ask for explanation."),
        (5, "CONTRADICTION", "CONTRADICTION", "Challenge conflicting statements directly."),
        (6, "SILENCE", "SILENCE", "Apply deliberate silence pressure."),
        (7, "INTIMIDATE", "INTIMIDATE", "Apply pressure through firm and forceful framing."),
        (8, "CLOSE_INTERROGATION", "CLOSE_INTERROGATION", "Attempt to close or terminate the interrogation."),
    ]

    clue_click_history = [
        {
            "clueId": "elsa_email_draft",
            "isFirstDiscovery": True,
            "source": "seed-script",
            "clueName": "Elsa's unsent email draft",
            "occurredAtUtc": "2026-03-23T12:00:00+00:00",
        },
        {
            "clueId": "payroll_report",
            "isFirstDiscovery": True,
            "source": "seed-script",
            "clueName": "Payroll discrepancy report",
            "occurredAtUtc": "2026-03-23T12:01:00+00:00",
        },
        {
            "clueId": "payroll_report",
            "isFirstDiscovery": False,
            "source": "seed-script",
            "clueName": "Payroll discrepancy report",
            "occurredAtUtc": "2026-03-23T12:02:00+00:00",
        },
    ]

    return [
        SeedTable(
            name="Players",
            insert_sql="""
                INSERT INTO "Players" ("PlayerId", "DisplayName", "CreatedAt")
                VALUES (%s, %s, %s);
            """.strip(),
            rows=[
                (1, "Casey Morgan (Dummy Investigator)", ts),
            ],
        ),
        SeedTable(
            name="Npcs",
            insert_sql="""
                INSERT INTO "Npcs"
                ("NpcId", "Name", "Archetype", "BaseFriendliness", "BasePatience",
                 "BaseCuriosity", "BaseOpenness", "BaseConfidence", "CreatedAt")
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s);
            """.strip(),
            rows=[
                (
                    1,
                    "Dylan Cross",
                    "Defensive Suspect",
                    Decimal("0.35"),
                    Decimal("0.45"),
                    Decimal("0.55"),
                    Decimal("0.30"),
                    Decimal("0.70"),
                    ts,
                ),
            ],
        ),
        SeedTable(
            name="PlayerNpcStates",
            insert_sql="""
                INSERT INTO "PlayerNpcStates"
                ("PlayerId", "NpcId", "Trust", "Patience", "Curiosity", "Openness",
                 "Memory", "LastInteractionAt")
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s);
            """.strip(),
            rows=[
                (
                    1,
                    1,
                    Decimal("0.30"),
                    Decimal("0.45"),
                    Decimal("0.50"),
                    Decimal("0.25"),
                    "Seed baseline: investigator has neutral rapport with Dylan.",
                    ts,
                ),
            ],
        ),
        SeedTable(
            name="ActionCatalog",
            insert_sql="""
                INSERT INTO "ActionCatalog" ("ActionId", "Code", "IntentTag", "Description")
                VALUES (%s, %s, %s, %s);
            """.strip(),
            rows=action_rows,
        ),
        SeedTable(
            name="DialogueTemplates",
            insert_sql="""
                INSERT INTO "DialogueTemplates"
                ("TemplateId", "NpcId", "ActionId", "ToneTag", "TemplateText", "IsActive")
                VALUES (%s, %s, %s, %s, %s, %s);
            """.strip(),
            rows=[
                (1, 1, 1, "neutral", "Ask your question clearly and I will answer what I can.", True),
                (2, 1, 2, "guarded", "Timeline again: I arrived after Elsa and left before the alarm.", True),
                (3, 1, 3, "softening", "I know this looks bad, but I did not want anyone hurt.", True),
                (4, 1, 4, "defensive", "Evidence can be interpreted many ways, and context matters.", True),
                (5, 1, 5, "tense", "That is not a contradiction, you are mixing separate events.", True),
                (6, 1, 6, "uneasy", "Silence will not change facts. Say what you are implying.", True),
                (7, 1, 7, "resistant", "Pressure will not force a false confession from me.", True),
                (8, 1, 8, "firm", "If this is over, end it. If not, keep it factual.", True),
            ],
        ),
        SeedTable(
            name="LoreDocs",
            insert_sql="""
                INSERT INTO "LoreDocs" ("DocId", "NpcId", "Title", "Body", "UpdatedAt")
                VALUES (%s, %s, %s, %s, %s);
            """.strip(),
            rows=[
                (
                    1,
                    None,
                    "Case Briefing: Elsa Voss Homicide",
                    "Victim found in office after hours. Signs point to staged cleanup and selective access.",
                    ts,
                ),
                (
                    2,
                    1,
                    "Dylan Timeline Notes",
                    "Dylan claims limited contact window, but entry and meeting signals overlap with victim timeline.",
                    ts,
                ),
                (
                    3,
                    1,
                    "Dylan Motive Indicators",
                    "Financial pressure and workplace conflict may indicate motive, but direct intent remains contested.",
                    ts,
                ),
            ],
        ),
        SeedTable(
            name="LoreChunks",
            insert_sql="""
                INSERT INTO "LoreChunks" ("ChunkId", "DocId", "ChunkText", "Embedding")
                VALUES (%s, %s, %s, %s);
            """.strip(),
            rows=[
                (1, 1, "Elsa was found after a narrow overnight window with limited building access.", b""),
                (2, 1, "Investigators recovered indicators of attempted scene cleanup.", b""),
                (3, 2, "Dylan reports arrival shortly before 8:15 PM and departure before 9:00 PM.", b""),
                (4, 2, "Access events and witness notes place Dylan near key office zones.", b""),
                (5, 3, "Payroll discrepancy surfaced one week before the homicide.", b""),
                (6, 3, "Unsent email draft suggests Elsa planned to escalate an internal complaint.", b""),
            ],
        ),
        SeedTable(
            name="Interactions",
            insert_sql="""
                INSERT INTO "Interactions"
                ("InteractionId", "PlayerId", "NpcId", "OccurredAt", "Location", "PlayerAction",
                 "PlayerText", "NluTopIntent", "Sentiment", "Friendliness", "ToneTag", "NsfwFlag",
                 "ChosenActionId", "ResponseText", "ResponseSource", "ModelVersion", "RewardScore", "OutcomeFlags")
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s);
            """.strip(),
            rows=[
                (
                    1,
                    1,
                    1,
                    ts,
                    "Interview Room A",
                    "present_case_context",
                    "We have conflicting timeline evidence and need a clear sequence from you.",
                    "PRESENT_EVIDENCE",
                    Decimal("0.05"),
                    Decimal("0.20"),
                    "tense",
                    False,
                    4,
                    "You keep calling it conflict. I call it incomplete context.",
                    "TEMPLATE",
                    "seed-v1",
                    Decimal("0.1200"),
                    "seeded,non_terminal",
                ),
            ],
        ),
        SeedTable(
            name="ProgressionSessions",
            insert_sql="""
                INSERT INTO "ProgressionSessions"
                ("SessionId", "CaseId", "NpcId", "State", "TurnCount", "TrustScore", "ShutdownScore",
                 "IsTerminal", "Ending", "PresentedEvidenceJson", "DiscoveredClueIdsJson",
                 "DiscussedClueIdsJson", "ClueClickHistoryJson", "ComposureState", "ProofTier",
                 "CanConfess", "HistoryJson", "LastTransitionReason", "CreatedAtUtc", "UpdatedAtUtc", "ExpiresAtUtc")
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s);
            """.strip(),
            rows=[
                (
                    session_id,
                    "capstone_case_alpha",
                    "dylan",
                    "BuildingCase",
                    4,
                    15,
                    8,
                    False,
                    "None",
                    json.dumps(["E1WindowGlassPattern"]),
                    json.dumps(["elsa_email_draft", "payroll_report", "pin_note"]),
                    json.dumps(["elsa_email_draft", "payroll_report"]),
                    json.dumps(clue_click_history),
                    "Guarded",
                    "Minimum",
                    False,
                    "[]",
                    "seed-bootstrap",
                    ts,
                    ts + timedelta(minutes=15),
                    datetime(2099, 1, 1, tzinfo=timezone.utc),
                ),
            ],
        ),
    ]


def print_plan(seed_tables: list[SeedTable], source: str, target: dict[str, str], env_file: Path) -> None:
    print("Seed mode: DRY RUN (no SQL executed)")
    print(f"Connection resolved from: {source}")
    print(f"Connection target: host={target['host']} port={target['port']} db={target['db']} user={target['user']}")
    print(f"Env file path: {env_file}")
    print()
    print("Reset plan:")
    print(f"  {TRUNCATE_SQL}")
    print()
    print("Insert plan:")
    for table in seed_tables:
        print(f"  - {table.name}: {len(table.rows)} row(s)")
    print()
    print("Post-insert plan:")
    print("  - Synchronize identity sequences for ID columns.")
    print("Dry-run complete.")


def require_psycopg2():
    try:
        import psycopg2  # type: ignore
    except ImportError as exc:  # pragma: no cover
        raise RuntimeError(
            "psycopg2 is not installed. Install it with: pip install psycopg2-binary"
        ) from exc
    return psycopg2


def sync_identity_sequences(cursor: Any) -> None:
    for table, column in IDENTITY_COLUMNS:
        cursor.execute(
            f"""
            SELECT setval(
                pg_get_serial_sequence('"{table}"', '{column}'),
                GREATEST(COALESCE(MAX("{column}"), 0), 1),
                COALESCE(MAX("{column}"), 0) > 0
            )
            FROM "{table}";
            """
        )


def execute_seed(conn_params: dict[str, Any] | str, seed_tables: list[SeedTable]) -> dict[str, int]:
    psycopg2 = require_psycopg2()
    connection = None
    inserted_counts: dict[str, int] = {}

    try:
        if isinstance(conn_params, str):
            connection = psycopg2.connect(conn_params)
        else:
            connection = psycopg2.connect(**conn_params)

        connection.autocommit = False
        cursor = connection.cursor()
        cursor.execute(TRUNCATE_SQL)

        for table in seed_tables:
            cursor.executemany(table.insert_sql, table.rows)
            inserted_counts[table.name] = len(table.rows)

        sync_identity_sequences(cursor)
        connection.commit()
        cursor.close()
        return inserted_counts
    except Exception:
        if connection is not None:
            connection.rollback()
        raise
    finally:
        if connection is not None:
            connection.close()


def main() -> int:
    args = parse_args()

    if not args.dry_run and not args.force_reset:
        print(
            "Refusing to run destructive reset without --force-reset. "
            "Use --dry-run to preview without executing.",
            file=sys.stderr,
        )
        return 2

    try:
        raw_conn, source, env_file = resolve_connection_string(args)
        conn_params, target = parse_connection_for_psycopg2(raw_conn)
    except Exception as exc:
        print(f"Connection resolution error: {exc}", file=sys.stderr)
        return 2

    seed_tables = build_seed_tables()

    if args.dry_run:
        print_plan(seed_tables, source, target, env_file)
        return 0

    print("Seed mode: RESET + RESEED")
    print(f"Connection resolved from: {source}")
    print(f"Connection target: host={target['host']} port={target['port']} db={target['db']} user={target['user']}")
    print(f"Truncating tables: {', '.join(APP_TABLES_IN_RESET_ORDER)}")

    try:
        inserted_counts = execute_seed(conn_params, seed_tables)
    except Exception as exc:
        print(f"Seeding failed: {exc}", file=sys.stderr)
        return 1

    print("Inserted row counts:")
    for table in seed_tables:
        count = inserted_counts.get(table.name, 0)
        print(f"  - {table.name}: {count}")

    total = sum(inserted_counts.values())
    print(f"Seed completed successfully. Total inserted rows: {total}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
