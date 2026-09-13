#!/usr/bin/env bash
# Fires N sequential requests at the gateway and reports the distribution across instances.
# Individual failures are counted as ERROR rather than aborting the run, so the report still
# appears while a destination is being evicted by health checks.
#
# Usage: ./scripts/test-load-balancing.sh [requests] [url]
set -uo pipefail

REQUESTS="${1:-20}"
URL="${2:-http://localhost:8000/products}"
HITS="$(mktemp)"
trap 'rm -f "$HITS"' EXIT

for i in $(seq 1 "$REQUESTS"); do
  instance="$(curl -fsS -o /dev/null -D - "$URL" 2>/dev/null | tr -d '\r' | awk 'tolower($1) == "x-instance-id:" { print $2 }')"
  instance="${instance:-ERROR}"
  printf '%4d  ->  %s\n' "$i" "$instance"
  echo "$instance" >> "$HITS"
done

echo
echo "Distribution over $REQUESTS requests:"
sort "$HITS" | uniq -c | awk -v total="$REQUESTS" '{ printf "  %-16s %4d  (%5.1f%%)\n", $2, $1, 100 * $1 / total }'
