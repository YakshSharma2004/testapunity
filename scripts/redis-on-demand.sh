#!/usr/bin/env bash
set -euo pipefail

COMPOSE_FILE="docker-compose.redis.yml"

if [[ ! -f "$COMPOSE_FILE" ]]; then
  echo "Missing $COMPOSE_FILE"
  exit 1
fi

case "${1:-}" in
  start)
    docker compose -f "$COMPOSE_FILE" up -d redis
    echo "Redis started."
    ;;
  stop)
    docker compose -f "$COMPOSE_FILE" stop redis
    echo "Redis stopped."
    ;;
  down)
    docker compose -f "$COMPOSE_FILE" down
    echo "Redis container and network removed."
    ;;
  status)
    docker compose -f "$COMPOSE_FILE" ps
    ;;
  logs)
    docker compose -f "$COMPOSE_FILE" logs -f redis
    ;;
  *)
    echo "Usage: $0 {start|stop|down|status|logs}"
    exit 1
    ;;
esac
