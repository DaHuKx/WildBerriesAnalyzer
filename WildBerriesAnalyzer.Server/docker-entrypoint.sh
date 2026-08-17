#!/bin/sh
set -eu

BROWSERS_PATH="${PLAYWRIGHT_BROWSERS_PATH:-/ms-playwright}"
mkdir -p "$BROWSERS_PATH"

# Chromium лежит в volume — качаем один раз при первом старте (не раздуваем image).
if ! find "$BROWSERS_PATH" -type f -name headless_shell 2>/dev/null | grep -q .; then
  echo "[playwright] Chromium не найден в $BROWSERS_PATH — установка…"
  NODE="$(find /app/.playwright/node -type f -name node | head -n 1)"
  if [ -z "$NODE" ]; then
    echo "[playwright] Ошибка: не найден драйвер node в /app/.playwright" >&2
    exit 1
  fi
  chmod +x "$NODE"
  "$NODE" /app/.playwright/package/cli.js install chromium
  echo "[playwright] Chromium установлен."
fi

exec dotnet WildBerriesAnalyzer.Server.dll "$@"
