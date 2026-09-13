# syntax=docker/dockerfile:1
# Build context is the repository root:
#   docker build -f src/Products/Dockerfile .

# ---- build -------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, with only the project file copied, so the restore layer is cached
# until the dependency list actually changes.
# .editorconfig carries the analyzer rules; the projects build with EnforceCodeStyleInBuild.
COPY [".editorconfig", "./"]
COPY ["src/Products/Products.csproj", "src/Products/"]
RUN dotnet restore "src/Products/Products.csproj"

COPY ["src/Products/", "src/Products/"]
RUN dotnet publish "src/Products/Products.csproj" \
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

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Run as the non-root user provided by the .NET base image.
USER $APP_UID

HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Products.dll"]
