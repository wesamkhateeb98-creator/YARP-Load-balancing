# syntax=docker/dockerfile:1
# Build context is the repository root:
#   docker build -f src/YarpGateway/Dockerfile .

# ---- build -------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# .editorconfig carries the analyzer rules; the projects build with EnforceCodeStyleInBuild.
COPY [".editorconfig", "./"]
COPY ["src/YarpGateway/YarpGateway.csproj", "src/YarpGateway/"]
RUN dotnet restore "src/YarpGateway/YarpGateway.csproj"

COPY ["src/YarpGateway/", "src/YarpGateway/"]
RUN dotnet publish "src/YarpGateway/YarpGateway.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:UseAppHost=false

# ---- runtime -----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# curl is needed only by the container HEALTHCHECK below.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

# appsettings.json pins Kestrel to http://localhost:8000 for the local, non-Docker
# workflow. Inside a container that would bind the loopback interface only and no
# published port would ever reach it, so compose overrides
# Kestrel__Endpoints__Gateway__Url to bind all interfaces on 8080.
EXPOSE 8080

USER $APP_UID

HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/gateway/health || exit 1

ENTRYPOINT ["dotnet", "YarpGateway.dll"]
