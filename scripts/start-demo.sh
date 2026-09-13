#!/usr/bin/env bash
# Builds the solution, then starts five Products instances (5000-5004) and the YARP gateway (8000).
# Logs land in .run/*.log and pids in .run/pids.txt (consumed by stop-demo.sh).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUN_DIR="$ROOT/.run"
PORTS=(5000 5001 5002 5003 5004)
GATEWAY_PORT=8000

mkdir -p "$RUN_DIR"
: > "$RUN_DIR/pids.txt"

if [[ "${1:-}" != "--skip-build" ]]; then
  echo "Building solution..."
  dotnet build "$ROOT/YarpLoadBalancing.slnx" -c Release --nologo -v q
fi

for port in "${PORTS[@]}"; do
  name="products-$port"
  dotnet run --project "$ROOT/src/Products/Products.csproj" -c Release --no-build -- \
    --urls "http://localhost:$port" --Instance:Name "$name" \
    > "$RUN_DIR/$name.log" 2>&1 &
  echo "$! $name" >> "$RUN_DIR/pids.txt"
  echo "  started $name"
done

dotnet run --project "$ROOT/src/YarpGateway/YarpGateway.csproj" -c Release --no-build -- \
  --urls "http://localhost:$GATEWAY_PORT" \
  > "$RUN_DIR/gateway.log" 2>&1 &
echo "$! gateway" >> "$RUN_DIR/pids.txt"
echo "  started gateway"

echo "Waiting for the gateway to accept traffic..."
for _ in $(seq 1 120); do
  if curl -fsS "http://localhost:$GATEWAY_PORT/products" > /dev/null 2>&1; then
    echo
    echo "Demo is up. Gateway: http://localhost:$GATEWAY_PORT/products"
    echo "Cluster state:  http://localhost:$GATEWAY_PORT/gateway/clusters"
    echo "Stop with:      ./scripts/stop-demo.sh"
    exit 0
  fi
  sleep 0.5
done

echo "Gateway did not become ready. Inspect $RUN_DIR for logs." >&2
exit 1
