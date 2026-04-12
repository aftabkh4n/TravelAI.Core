FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["samples/TravelAI.Api/TravelAI.Api.csproj", "samples/TravelAI.Api/"]
COPY ["src/TravelAI.Core/TravelAI.Core.csproj", "src/TravelAI.Core/"]
RUN dotnet restore "samples/TravelAI.Api/TravelAI.Api.csproj"
COPY . .
RUN dotnet publish "samples/TravelAI.Api/TravelAI.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TravelAI.Api.dll"]