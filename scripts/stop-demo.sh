#!/usr/bin/env bash
# Stops every process started by start-demo.sh.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PIDS_FILE="$ROOT/.run/pids.txt"

if [[ ! -f "$PIDS_FILE" ]]; then
  echo "Nothing to stop: .run/pids.txt not found."
  exit 0
fi

while read -r pid name; do
  [[ -z "${pid:-}" ]] && continue
  if kill "$pid" 2> /dev/null; then
    echo "  stopped $name (pid $pid)"
  else
    echo "  $name (pid $pid) was not running"
  fi
done < "$PIDS_FILE"

rm -f "$PIDS_FILE"
echo "Demo stopped."
