# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY . .

RUN dotnet publish samples/TravelAI.Api/TravelAI.Api.csproj \
    --configuration Release \
    --output /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN groupadd -r appgroup && useradd -r -g appgroup appuser
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TravelAI.Api.dll"]
```

Also open `deploy/.dockerignore` and remove the `**/obj` line — that was blocking the restored packages from being available:
```
**/.git
**/.gitignore
**/bin
**/*.user
**/.vs
**/node_modules
**/TestResults
**/artifacts
README.md
.github