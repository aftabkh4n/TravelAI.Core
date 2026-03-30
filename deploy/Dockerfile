# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Copy project files first for layer caching
COPY TravelAI.Core.sln ./
COPY src/TravelAI.Core/TravelAI.Core.csproj              src/TravelAI.Core/
COPY samples/TravelAI.Api/TravelAI.Api.csproj             samples/TravelAI.Api/
COPY tests/TravelAI.Core.Tests/TravelAI.Core.Tests.csproj tests/TravelAI.Core.Tests/

RUN dotnet restore

COPY src/      src/
COPY samples/  samples/

RUN dotnet publish samples/TravelAI.Api/TravelAI.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN groupadd -r appgroup && useradd -r -g appgroup appuser
USER appuser

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -f http://localhost:8080/health/live || exit 1

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TravelAI.Api.dll"]
