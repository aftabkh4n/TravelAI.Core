FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/TravelAI.SearchWorker/TravelAI.SearchWorker.csproj", "src/TravelAI.SearchWorker/"]
COPY ["src/TravelAI.Core/TravelAI.Core.csproj", "src/TravelAI.Core/"]
RUN dotnet restore "src/TravelAI.SearchWorker/TravelAI.SearchWorker.csproj"
COPY . .
RUN dotnet publish "src/TravelAI.SearchWorker/TravelAI.SearchWorker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TravelAI.SearchWorker.dll"]