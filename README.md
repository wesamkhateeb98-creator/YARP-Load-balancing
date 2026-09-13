<h1 align="center">YARP Load Balancing — .NET Reference Demo</h1>

<p align="center">
  A runnable, production-shaped demonstration of every load-balancing algorithm in YARP,<br/>
  routing a single gateway across five live <code>Products</code> API instances.
</p>

<p align="center">
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white">
  <img alt="YARP 2.3.0" src="https://img.shields.io/badge/YARP-2.3.0-0078D4">
  <img alt="C#" src="https://img.shields.io/badge/C%23-13-239120?logo=csharp&logoColor=white">
  <img alt="Docker Compose" src="https://img.shields.io/badge/Docker-Compose%20v2-2496ED?logo=docker&logoColor=white">
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey">
</p>

---

## Introduction

**YARP** — *Yet Another Reverse Proxy* — is Microsoft's reverse proxy toolkit for .NET. It is not a packaged appliance like NGINX or HAProxy; it is a **library you host inside your own ASP.NET Core application**. You get routing, load balancing, health checking, session affinity and request/response transforms out of the box, and because the whole pipeline is ordinary ASP.NET Core middleware, anything it does not do you can write in C# and drop into the same request path.

That design is what makes it a natural **API Gateway**. A gateway is the single front door to a fleet of services: it terminates TLS, authenticates the caller, enforces rate limits, rewrites paths, and then picks *which* backend instance actually handles the request. YARP handles the plumbing at close to the throughput of a native proxy — it forwards on `System.IO.Pipelines` with pooled buffers and HTTP/2 multiplexing — while leaving the policy decisions in your language, your solution, your tests and your CI pipeline.

**Load balancing is the part of that job that decides where each request goes**, and it is where a gateway earns or loses its keep:

- **Horizontal scale.** Five instances only deliver five instances' worth of throughput if traffic actually reaches all five. A poor distribution leaves capacity idle while a hot instance saturates.
- **Tail latency.** P99 is dominated by the *unluckiest* requests. A policy that avoids busy instances cuts the tail far more than adding hardware does.
- **Resilience.** Combined with health checks, load balancing drains traffic from a failing instance before users notice — and restores it automatically when the instance recovers.
- **Cost.** Balancing well is the difference between running five right-sized instances and over-provisioning eight to absorb the imbalance.

The choice of algorithm is a genuine engineering trade-off, not a default to accept unexamined. Round Robin is perfectly fair and completely blind to load. Least Requests is maximally responsive and does nothing at all without concurrency. Power of Two Choices buys most of the benefit of both for O(1) cost. **This repository makes those trade-offs observable**: every instance reports which one it is, so you can switch policy, fire requests, and watch the distribution change.

## Demo Architecture Overview

Five identical instances of a minimal-API `Products` service run on ports **5000–5004**. Each stamps its own identity onto every response — in the JSON body as `servedBy`, and in an `X-Instance-Id` header — so the routing decision is visible from plain `curl`.

A separate **`YarpGateway`** project listens on port **8000** and reverse-proxies `/products` across all five, rewriting the path to `/api/products` on the way through. Active health checks probe `/health` every 10 seconds and evict an instance after two consecutive failures, so stopping a backend is a first-class part of the demo rather than a crash.

The whole thing runs under **Docker Compose** — five backend containers, the gateway, and a throwaway load-test container. The load-balancing algorithm is a single value in [`.env`](.env): change it, re-create the gateway, and compare the distribution. Running the projects directly with the .NET SDK still works and adds live config hot-reload; both paths are covered below.

```mermaid
flowchart LR
    CLI["Client<br/>curl · Postman · browser"]
    GW["YarpGateway :8000<br/>route /products/**<br/>PathRemovePrefix + PathPrefix<br/>LoadBalancingPolicy"]

    CLI -- "GET /products" --> GW

    GW -- "GET /api/products" --> P0["Products<br/>:5000"]
    GW -- "GET /api/products" --> P1["Products<br/>:5001"]
    GW -- "GET /api/products" --> P2["Products<br/>:5002"]
    GW -- "GET /api/products" --> P3["Products<br/>:5003"]
    GW -- "GET /api/products" --> P4["Products<br/>:5004"]

    GW -. "active health probe /health<br/>every 10s, evict after 2 failures" .-> P0
    GW -.-> P1
    GW -.-> P2
    GW -.-> P3
    GW -.-> P4

    P0 -- "X-Instance-Id: products-5000" --> GW
    P1 -- "X-Instance-Id: products-5001" --> GW
    P2 -- "X-Instance-Id: products-5002" --> GW
    P3 -- "X-Instance-Id: products-5003" --> GW
    P4 -- "X-Instance-Id: products-5004" --> GW

    GW --> CLI
```

**Repository layout**

```
YARP-Load-balancing/
├─ src/
│  ├─ Products/                         Minimal-API backend, run 5× on 5000-5004
│  │  ├─ Models/                        Product, InstanceInfo, ProductsResponse
│  │  ├─ Services/                      Catalogue + instance identity resolution
│  │  ├─ Program.cs                     GET /api/products, GET /health
│  │  └─ Dockerfile                     Multi-stage build, non-root, HEALTHCHECK
│  └─ YarpGateway/                      The reverse proxy, :8000
│     ├─ LoadBalancing/                 Custom WeightedRoundRobin policy
│     ├─ appsettings.json               Routes, cluster, destinations, policy
│     ├─ Program.cs                     AddReverseProxy + GET /gateway/clusters
│     └─ Dockerfile                     Multi-stage build, non-root, HEALTHCHECK
├─ docs/algorithms/                     One deep-dive per algorithm
├─ docker-compose.yml                   5 backends + gateway + loadtest profile
├─ .env                                 Algorithm switch, request count, latency knob
└─ .dockerignore                        Keeps the build context minimal
```

**Endpoints**

| Endpoint | Serves |
|----------|--------|
| `GET http://localhost:8000/products` | Load-balanced product catalogue, via the gateway |
| `GET http://localhost:8000/gateway/clusters` | Live policy, destination health and in-flight request counts |
| `GET http://localhost:8000/gateway/health` | Gateway liveness |
| `GET http://localhost:500X/api/products` | A single instance directly, bypassing the gateway |
| `GET http://localhost:500X/health` | Probed by the gateway's active health checks |

## Load Balancing Algorithms

Every algorithm below is documented in depth — internals, a Mermaid flow diagram, a five-request walkthrough, use cases, trade-offs and working configuration. **Each write-up includes the distribution actually measured against this running cluster**, including the cases where a policy does something other than what its name suggests.

| Algorithm | In one line | Cost | Load-aware | Deterministic | Deep dive |
|-----------|-------------|------|:----------:|:-------------:|-----------|
| **Round Robin** | Cycles through destinations with a per-cluster atomic counter — perfectly even, totally load-blind. | O(1) | No | Yes | [round-robin.md](docs/algorithms/round-robin.md) |
| **Power of Two Choices** | Samples two destinations at random, takes the less busy. YARP's **default**; near-optimal balance at O(1). | O(1) | Yes | No | [power-of-two-choices.md](docs/algorithms/power-of-two-choices.md) |
| **Least Requests** | Scans all destinations and picks the fewest in-flight. Most responsive — and inert without concurrency. | O(n) | Yes | Yes | [least-requests.md](docs/algorithms/least-requests.md) |
| **Random** | One uniform draw, zero state. Cheapest and most scalable; visibly lumpy over short runs. | O(1) | No | No | [random.md](docs/algorithms/random.md) |
| **First Alphabetical** | Always the lowest destination ID that is healthy. Not balancing — active/passive **failover**. | O(n) | No | Yes | [first-alphabetical.md](docs/algorithms/first-alphabetical.md) |
| **Custom** | Implement `ILoadBalancingPolicy`. This repo ships a working **WeightedRoundRobin**. | Yours | Yours | Yours | [custom.md](docs/algorithms/custom.md) |

> **Choosing one:** default to **Power of Two Choices** unless you have a specific reason not to. Use **Round Robin** for homogeneous instances and uniform request costs, **Least Requests** for long-running or highly variable work under real concurrency, **First Alphabetical** for primary/standby failover, and a **Custom** policy when instances differ in capacity or the decision depends on the request itself.

## How to Run the Demo

Everything runs in Docker: five backend containers, the gateway, and a throwaway load-test container. No .NET SDK required on your machine.

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) with Compose v2 — verify with `docker compose version`
- Ports **5000–5004** and **8000** free on the host
- `curl` (bundled with Windows 10+, macOS and Linux), or Postman

### 1. Clone and start

```bash
git clone https://github.com/wesamkhateeb98-creator/YARP-Load-balancing.git
cd YARP-Load-balancing
docker compose up -d --build
```

Compose builds both images, starts the five `Products` containers, waits until each one reports healthy, then starts the gateway. First build pulls the .NET SDK and runtime images, so expect a few minutes; subsequent builds are cached.

```bash
docker compose ps
```

```
NAME             STATUS                    PORTS
products-5000    Up 30 seconds (healthy)   0.0.0.0:5000->8080/tcp
products-5001    Up 30 seconds (healthy)   0.0.0.0:5001->8080/tcp
products-5002    Up 30 seconds (healthy)   0.0.0.0:5002->8080/tcp
products-5003    Up 30 seconds (healthy)   0.0.0.0:5003->8080/tcp
products-5004    Up 30 seconds (healthy)   0.0.0.0:5004->8080/tcp
yarp-gateway     Up 18 seconds (healthy)   0.0.0.0:8000->8080/tcp
```

Each container listens on **8080** internally and is published on its own host port, so the 5000–5004 story holds from outside. Inside the Docker network the gateway reaches the backends by service name — `http://products-5000:8080/` — which is exactly what [`docker-compose.yml`](docker-compose.yml) overrides on the cluster's destinations.

### 2. Send a request through the gateway

```bash
curl -i http://localhost:8000/products
```

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
X-Instance-Id: products-5000

{
  "servedBy": {
    "id": "products-5000",
    "host": "3f2a91c4e8d7",
    "port": 8080,
    "processId": 1,
    "startedAtUtc": "2026-09-13T09:41:12.4180000+00:00"
  },
  "products": [
    { "id": 1, "name": "Mechanical Keyboard", "category": "Peripherals", "price": 129.99, "stockQuantity": 42 },
    { "id": 2, "name": "27\" 4K Monitor", "category": "Displays", "price": 379.00, "stockQuantity": 18 }
  ],
  "count": 6
}
```

The `servedBy` block and the `X-Instance-Id` header name the instance that handled this specific request — that is the whole point of the demo. Under Docker, `host` is the container ID and `port` is the container-internal 8080 for every instance; the identity that distinguishes them is `id`, set from `Instance__Name` in compose.

### 3. Watch the load balancing

A `loadtest` container in the `tools` profile fires a batch of requests and prints the distribution. It is not started by `docker compose up`:

```bash
docker compose --profile tools run --rm loadtest
```

```
   1  ->  products-5000
   2  ->  products-5001
   3  ->  products-5002
   4  ->  products-5003
   5  ->  products-5004
   6  ->  products-5000
   ...

Distribution over 25 requests:
  products-5000       5  ( 20.0%)
  products-5001       5  ( 20.0%)
  products-5002       5  ( 20.0%)
  products-5003       5  ( 20.0%)
  products-5004       5  ( 20.0%)
```

Change the request count with `REQUESTS`, either in [`.env`](.env) or inline:

```bash
REQUESTS=100 docker compose --profile tools run --rm loadtest
```

Under the default `RoundRobin` policy the cycle is exact. The *starting* instance depends on the internal ordering of the destination list rather than the order in `appsettings.json`, so your first line may differ — the cycle and the even split will not.

**Or a one-liner from the host, no containers:**

```bash
for i in $(seq 1 25); do
  curl -s http://localhost:8000/products | grep -o '"id":"products-[0-9]*"'
done | sort | uniq -c
```

### 4. Switch the algorithm

Edit `LB_POLICY` in [`.env`](.env):

```dotenv
LB_POLICY=PowerOfTwoChoices
```

Then re-create just the gateway — roughly two seconds, the backends keep running:

```bash
docker compose up -d gateway
```

Confirm what it actually loaded, then re-run step 3 and compare:

```bash
curl -s http://localhost:8000/gateway/clusters
```

Valid values: `RoundRobin`, `PowerOfTwoChoices`, `Random`, `LeastRequests`, `FirstAlphabetical`, and the custom `WeightedRoundRobin`. Compose injects this as an environment variable that overrides the cluster's `LoadBalancingPolicy`; everything else about the cluster — health checks, destination weights — still comes from [`src/YarpGateway/appsettings.json`](src/YarpGateway/appsettings.json).

> **Two results worth reproducing**, because they contradict the names: under **`LeastRequests`**, 15 *sequential* requests all land on one instance — with no concurrency every in-flight count ties at zero, so the strict comparison always returns the first destination. Under **`FirstAlphabetical`**, every request lands on `products-5000` by design; it is a failover policy, not a balancing one. Both are explained in their deep dives.

### 5. Test health-check failover

Stop one backend and watch YARP route around it:

```bash
docker compose stop products-5002

curl -s http://localhost:8000/gateway/clusters      # products-5002 -> "Unhealthy" after two probe cycles
docker compose --profile tools run --rm loadtest    # traffic now splits four ways, 25% each
```

Bring it back and the next probe returns it to the rotation automatically, with no gateway restart:

```bash
docker compose start products-5002
```

### 6. See a load-aware policy actually react

Load-aware policies need concurrency *and* uneven request cost to differentiate. Slow one instance down via [`.env`](.env):

```dotenv
LB_POLICY=LeastRequests
PRODUCTS_5002_LATENCY_MS=400
```

```bash
docker compose up -d gateway products-5002
```

Then drive parallel load — a single sequential loop will not show anything:

```bash
docker compose --profile tools run --rm \
  -e REQUESTS=60 loadtest
```

```powershell
# Or concurrently from the host, which is what actually exercises the policy
1..60 | ForEach-Object -Parallel {
    (Invoke-RestMethod 'http://localhost:8000/products').servedBy.id
} -ThrottleLimit 12 | Group-Object | Sort-Object Name
```

`products-5002` accumulates in-flight requests and receives a visibly smaller share.

### 7. Logs and shutdown

```bash
docker compose logs -f gateway          # YARP routing and health-check decisions
docker compose logs -f products-5000    # a single backend

docker compose down                     # stop and remove containers + network
docker compose down --rmi local -v      # also remove the images built here
```

<details>
<summary><strong>Running without Docker</strong> — .NET SDK 10 directly on the host</summary>

The projects run standalone; only the destination addresses differ, and `appsettings.json` already points at `http://localhost:500X/`.

```bash
dotnet build YarpLoadBalancing.slnx -c Release
```

Terminals 1–5, one per instance:

```bash
dotnet run --project src/Products/Products.csproj -c Release -- --urls http://localhost:5000
dotnet run --project src/Products/Products.csproj -c Release -- --urls http://localhost:5001
dotnet run --project src/Products/Products.csproj -c Release -- --urls http://localhost:5002
dotnet run --project src/Products/Products.csproj -c Release -- --urls http://localhost:5003
dotnet run --project src/Products/Products.csproj -c Release -- --urls http://localhost:5004
```

Terminal 6, the gateway — it binds `http://localhost:8000` from its own `Kestrel` settings:

```bash
dotnet run --project src/YarpGateway/YarpGateway.csproj -c Release
```

Each instance derives its ID from the port it bound to, so no extra configuration is needed. Visual Studio and Rider users can pick the `products-5000` … `products-5004` launch profiles instead.

Outside Docker the policy is **hot-reloadable**: edit `LoadBalancingPolicy` in [`src/YarpGateway/appsettings.json`](src/YarpGateway/appsettings.json), save, and `LoadFromConfig` re-applies it to live traffic with no restart and no dropped requests.

</details>

### Using Postman

Import a single request — `GET http://localhost:8000/products` — and add this to its **Tests** tab to log the serving instance on every send:

```javascript
pm.test("Served by a known instance", () => {
    const instance = pm.response.headers.get("X-Instance-Id");
    console.log("served by:", instance);
    pm.expect(instance).to.match(/^products-500[0-4]$/);
});
```

Hit **Send** repeatedly, or use the Collection Runner with 25 iterations, and watch the console cycle through the ports.

---

## Further Reading

- [YARP documentation](https://microsoft.github.io/reverse-proxy/) — official reference
- [YARP load balancing reference](https://microsoft.github.io/reverse-proxy/articles/load-balancing.html)
- [Destination health checks](https://microsoft.github.io/reverse-proxy/articles/dests-health-checks.html)
- Mitzenmacher, *The Power of Two Choices in Randomized Load Balancing* (1996) — the result behind YARP's default policy
