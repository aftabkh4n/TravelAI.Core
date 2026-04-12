# TravelAI.Core

[![CI](https://github.com/aftabkh4n/TravelAI.Core/actions/workflows/ci.yml/badge.svg)](https://github.com/aftabkh4n/TravelAI.Core/actions)
[![Deploy](https://github.com/aftabkh4n/TravelAI.Core/actions/workflows/deploy.yml/badge.svg)](https://github.com/aftabkh4n/TravelAI.Core/actions)
[![NuGet](https://img.shields.io/nuget/v/TravelAI.Core.svg)](https://www.nuget.org/packages/TravelAI.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com)

A .NET 10 library for building AI-native travel platforms. Wraps Azure OpenAI,
Azure AI Search, and booking automation into clean, testable domain services.
Upgraded to a full multi-service architecture with RabbitMQ messaging, 
Serilog structured logging, and OpenTelemetry distributed tracing.

## What it does

The platform has four core capabilities exposed through clean interfaces:

**Itinerary generation** takes a traveller profile and destination, calls Azure
OpenAI GPT-4o, and returns a fully structured itinerary with day-by-day
activities, accommodation, and dining recommendations.

**Destination search** uses Azure AI Search with semantic ranking to find
relevant destinations from natural language queries — "somewhere warm in
Europe under £800" returns meaningful results rather than keyword matches.

**Price anomaly detection** analyses a batch of flight options and flags
anything statistically unusual — a £200 flight on a route that normally
costs £600 gets flagged automatically.

**Booking automation** takes a confirmed itinerary and executes the booking
flow end to end, returning a booking reference or a structured failure reason.

## Architecture

The system runs as three independent services connected through RabbitMQ:
POST /api/destinations/search
→ publishes SearchRequested to RabbitMQ
→ SearchWorker picks it up, calls Azure AI Search
→ publishes SearchCompleted
POST /api/itinerary/generate
→ publishes ItineraryRequested to RabbitMQ
→ AiWorker picks it up, calls Azure OpenAI GPT-4o
→ publishes ItineraryGenerated

This means the API returns in under 100ms regardless of how long the AI
call takes. Long-running operations don't block the HTTP response.

## Services

| Service | What it does |
|---|---|
| `TravelAI.Api` | HTTP gateway — receives requests, publishes to queue |
| `TravelAI.SearchWorker` | Consumes search requests, calls Azure AI Search |
| `TravelAI.AiWorker` | Consumes itinerary requests, calls Azure OpenAI |

## Tech stack

| Layer | Technology |
|---|---|
| Framework | .NET 10 |
| AI | Azure OpenAI (GPT-4o) |
| Search | Azure AI Search (semantic ranking) |
| Messaging | RabbitMQ via MassTransit |
| Logging | Serilog (structured JSON) |
| Tracing | OpenTelemetry |
| API docs | Scalar |
| Health checks | ASP.NET Core health checks with Azure connectivity checks |

## Running locally

**Prerequisites:** .NET 10 SDK, Docker Desktop, Azure subscription

```bash
# Start RabbitMQ
docker run -d --name travelai-rabbitmq \
  -p 5672:5672 -p 15672:15672 \
  rabbitmq:3-management

# Copy config template and fill in your Azure credentials
cp samples/TravelAI.Api/appsettings.sample.json \
   samples/TravelAI.Api/appsettings.json

# Run all three services (separate terminals)
dotnet run --project samples/TravelAI.Api
dotnet run --project src/TravelAI.SearchWorker
dotnet run --project src/TravelAI.AiWorker
```

RabbitMQ management UI: http://localhost:15672 (guest/guest)
API docs: http://localhost:5000/scalar/v1

## Running with Docker Compose

```bash
docker-compose up --build
```

All services start in the correct order. RabbitMQ health check ensures
workers only connect once the broker is ready.

## API endpoints

| Method | Endpoint | Behaviour |
|---|---|---|
| POST | `/api/destinations/search` | Queues search, returns correlation ID |
| POST | `/api/itinerary/generate` | Queues AI generation, returns correlation ID |
| POST | `/api/itinerary/refine` | Synchronous — refines an existing itinerary |
| POST | `/api/flights/analyse` | Synchronous — detects price anomalies |
| POST | `/api/bookings` | Executes automated booking |
| DELETE | `/api/bookings/{ref}` | Cancels a booking |
| GET | `/health/live` | Liveness probe |
| GET | `/health/ready` | Readiness probe (checks Azure connectivity) |

## Project structure
TravelAI.Core/
├── src/
│   ├── TravelAI.Core/           # Domain library — interfaces, models, services
│   │   ├── Interfaces/          # IItineraryGenerationService, IDestinationSearchService, etc.
│   │   ├── Messages/            # RabbitMQ message contracts
│   │   ├── Models/              # Domain models
│   │   ├── Services/            # Azure integrations
│   │   ├── Extensions/          # AddTravelAI() DI registration
│   │   ├── HealthChecks/        # Azure connectivity health checks
│   │   └── Middleware/          # Observability middleware
│   ├── TravelAI.SearchWorker/   # Background worker — search via RabbitMQ
│   └── TravelAI.AiWorker/       # Background worker — AI generation via RabbitMQ
├── samples/
│   └── TravelAI.Api/            # ASP.NET Core API gateway
├── tests/
│   └── TravelAI.Core.Tests/     # Unit tests
└── docker-compose.yml

## NuGet package

The core library is available as a standalone NuGet package:

[![NuGet](https://img.shields.io/nuget/v/TravelAI.Core.svg)](https://www.nuget.org/packages/TravelAI.Core)

```bash
dotnet add package TravelAI.Core
```

Use it in any .NET project with two lines:

```csharp
builder.Services.AddTravelAI(builder.Configuration);
```

Then inject any of the interfaces:

```csharp
app.MapPost("/itinerary", async (
    GenerateRequest req,
    IItineraryGenerationService generator) =>
    Results.Ok(await generator.GenerateAsync(...)));
```

---

## License

MIT