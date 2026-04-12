FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/TravelAI.AiWorker/TravelAI.AiWorker.csproj", "src/TravelAI.AiWorker/"]
COPY ["src/TravelAI.Core/TravelAI.Core.csproj", "src/TravelAI.Core/"]
RUN dotnet restore "src/TravelAI.AiWorker/TravelAI.AiWorker.csproj"
COPY . .
RUN dotnet publish "src/TravelAI.AiWorker/TravelAI.AiWorker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TravelAI.AiWorker.dll"]
