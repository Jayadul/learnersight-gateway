FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY NetZeroGateway.Domain/NetZeroGateway.Domain.csproj           NetZeroGateway.Domain/
COPY NetZeroGateway.Infrastructure/NetZeroGateway.Infrastructure.csproj   NetZeroGateway.Infrastructure/
COPY NetZeroGateway.Application/NetZeroGateway.Application.csproj NetZeroGateway.Application/
COPY NetZeroGateway.API/NetZeroGateway.API.csproj                 NetZeroGateway.API/
# Restore the API and its transitive project references only — NOT the solution.
# A solution-wide restore fails the moment a project exists that this image has no
# reason to copy (the test project), and the runtime image should never pull test
# dependencies anyway. Do not reintroduce `COPY *.sln` + bare `dotnet restore`.
RUN dotnet restore NetZeroGateway.API
COPY . .
RUN dotnet publish NetZeroGateway.API -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
# curl is here solely for HEALTHCHECK below — the runtime image ships no HTTP client.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
# Media Library storage — owned by the app user so the mounted volume is writable
RUN mkdir -p /app/media && chown app:app /app/media
# Run as the non-root user provided by the .NET runtime image
USER app
EXPOSE 8080
# Migrations and seeding both finish before the app starts listening, so answering
# /health at all means the gateway is ready to serve. start-period covers a first run
# against an empty database, where every migration has to be applied before that point.
HEALTHCHECK --interval=10s --timeout=3s --start-period=90s --retries=5 \
  CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "NetZeroGateway.API.dll"]
