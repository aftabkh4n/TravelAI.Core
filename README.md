# TravelAI.Core

[![CI](https://github.com/aftabkh4n/TravelAI.Core/actions/workflows/ci.yml/badge.svg)](https://github.com/aftabkh4n/TravelAI.Core/actions)
[![Deploy](https://github.com/aftabkh4n/TravelAI.Core/actions/workflows/deploy.yml/badge.svg)](https://github.com/aftabkh4n/TravelAI.Core/actions)
[![NuGet](https://img.shields.io/nuget/v/TravelAI.Core.svg)](https://www.nuget.org/packages/TravelAI.Core)
[![NuGet Downloads](https://img.shields.io/nuget/dt/TravelAI.Core.svg)](https://www.nuget.org/packages/TravelAI.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com)

A .NET 10 library and multi-service platform for building AI-native travel applications. Supports OpenAI, Anthropic, Ollama, and Azure OpenAI — swap providers without changing any application code. Ships with a three-service architecture using RabbitMQ messaging and Serilog centralized logging, deployable locally via Docker Compose or to Azure Kubernetes Service.

---

## Installation

```bash
dotnet add package TravelAI.Core
```

---

## Quick start

### Zero credentials — try it instantly

```csharp
builder.Services.AddTravelAI(options => options.UseMock());
```

No API keys, no Azure account needed. Returns realistic itineraries, destinations, and booking results. Good for local development and testing before committing to a provider.

### OpenAI

```csharp
builder.Services.AddTravelAI(options =>
    options.UseOpenAI("sk-..."));
```

### Anthropic (Claude)

```csharp
builder.Services.AddTravelAI(options =>
    options.UseAnthropic("sk-ant-..."));
```

### Ollama — free and local

```csharp
builder.Services.AddTravelAI(options =>
    options.UseOllama("http://localhost:11434", "llama3.2"));
```

### Azure OpenAI

```csharp
builder.Services.AddTravelAI(options =>
    options.UseAzureOpenAI(
        "https://your-resource.openai.azure.com/",
        "YOUR_KEY",
        "gpt-4o"));
```

The `IItineraryGenerationService`, `IPriceAnomalyDetector`, and `IDestinationSearchService` interfaces stay identical across all providers. Swap backends without changing any other code.

---

## What it does

| Service | Description |
|---|---|
| `IItineraryGenerationService` | Generates day-by-day itineraries with cost estimates, tailored to the traveller's preferences and tier |
| `IPriceAnomalyDetector` | Flags flight price anomalies — surges, unexpected deals, seasonal deviations — using statistical analysis against historical baselines |
| `IDestinationSearchService` | Semantic search over destinations. Understands queries like *"warm with beaches and good food, not too touristy"* |
| `IBookingAutomationService` | Orchestrates end-to-end bookings with retry logic, partial booking handling, and automatic rollback on failure |

---

## Usage

```csharp
// Generate a personalised itinerary
var itinerary = await generator.GenerateAsync(
    new TravellerProfile
    {
        Name = "Aftab",
        Email = "a@example.com",
        Preferences = ["food", "history", "architecture"],
        Tier = TravelTier.Premium
    },
    destination: "Rome, Italy",
    departure: DateOnly.Parse("2025-08-01"),
    returnDate: DateOnly.Parse("2025-08-08"),
    additionalInstructions: "Avoid tourist traps, focus on local food");

// Semantic destination search
var results = await search.SearchAsync(
    "warm Mediterranean with history and local food", maxResults: 5);

// Detect price anomalies in flight results
await foreach (var flight in detector.AnalyseBatchAsync(flights))
{
    if (flight.PriceAnomaly?.Type == AnomalyType.UnexpectedDeal)
        Console.WriteLine($"Great deal: {flight.Origin}→{flight.Destination} at £{flight.PriceGbp}");
}

// Execute a booking
var result = await bookingService.BookAsync(confirmedItinerary);
Console.WriteLine(result.BookingReference);
```

---

## Multi-service architecture

The platform runs as three independent services connected through RabbitMQ. The API returns in under 100ms regardless of how long the AI call takes — long-running operations don't block the HTTP response.

```
POST /api/destinations/search
  → API publishes SearchRequested to RabbitMQ (returns 202 + correlation ID)
  → SearchWorker consumes, calls IDestinationSearchService
  → SearchWorker publishes SearchCompleted

POST /api/itinerary/generate
  → API publishes ItineraryRequested to RabbitMQ (returns 202 + correlation ID)
  → AiWorker consumes, calls IItineraryGenerationService
  → AiWorker publishes ItineraryGenerated
```

| Service | Role |
|---|---|
| `TravelAI.Api` | HTTP gateway — receives requests, validates, publishes to queue |
| `TravelAI.SearchWorker` | Consumes search requests, calls destination search, publishes results |
| `TravelAI.AiWorker` | Consumes itinerary requests, calls AI provider, publishes results |

All three services write structured JSON logs via Serilog. Log files roll daily and are written to a shared `logs/` directory.

---

## Running locally with Docker Compose

```bash
docker-compose up --build
```

All services start in the correct order. RabbitMQ health check ensures workers only connect once the broker is ready.

| URL | What it is |
|---|---|
| `http://localhost:5000` | TravelAI API |
| `http://localhost:15672` | RabbitMQ management UI (guest/guest) |

---

## Project structure

```
TravelAI.Core/
├── src/
│   ├── TravelAI.Core/           # Domain library — interfaces, models, services, providers
│   │   ├── Interfaces/          # IItineraryGenerationService, IDestinationSearchService, ...
│   │   ├── Models/              # Itinerary, FlightOption, PriceAnomaly, BookingResult, ...
│   │   ├── Messages/            # RabbitMQ message contracts
│   │   ├── Providers/           # ILlmProvider — OpenAI, Anthropic, Ollama, AzureOpenAI, Mock
│   │   ├── Services/            # Provider-agnostic service implementations
│   │   ├── Middleware/          # Observability middleware + AI rate limiting
│   │   ├── HealthChecks/        # Azure OpenAI and AI Search health probes
│   │   └── Extensions/          # AddTravelAI() DI registration
│   ├── TravelAI.Api/            # ASP.NET Core 10 minimal API gateway
│   ├── TravelAI.SearchWorker/   # Background worker — search via RabbitMQ
│   └── TravelAI.AiWorker/       # Background worker — AI generation via RabbitMQ
├── tests/
│   └── TravelAI.Core.Tests/     # xUnit + FluentAssertions
├── deploy/                      # Dockerfile, Kubernetes manifests, GitHub Actions CI/CD
└── docker-compose.yml
```

**Provider architecture:**

```
IItineraryGenerationService
        │
        ▼
  ILlmProvider
        │
   ┌────┴──────────────────────────────────────┐
   │           │            │         │        │
 OpenAI   Anthropic      Ollama   AzureOpenAI Mock
```

---

## API endpoints

| Method | Endpoint | Behaviour |
|---|---|---|
| `POST` | `/api/itinerary/generate` | Queues AI generation, returns 202 + correlation ID |
| `POST` | `/api/itinerary/refine` | Synchronous — refines an existing itinerary |
| `GET` | `/api/destinations/search?q=...` | Queues search, returns 202 + correlation ID |
| `POST` | `/api/flights/analyse` | Synchronous — detects price anomalies |
| `POST` | `/api/bookings` | Execute automated booking |
| `DELETE` | `/api/bookings/{reference}` | Cancel a booking |
| `GET` | `/health/live` | Liveness probe |
| `GET` | `/health/ready` | Readiness probe (checks Azure OpenAI + Search) |

---

## Deployment to AKS

Deployed to Azure Kubernetes Service via GitHub Actions. Every push to `main` builds, tests, pushes the Docker image to GHCR, and deploys with a manual approval gate before production.

```bash
# Provision all Azure resources from scratch
bash deploy/scripts/provision-azure.sh

# Push to main to trigger the pipeline
git push origin main
```

See [`deploy/DEPLOYMENT.md`](deploy/DEPLOYMENT.md) for the full guide.

---

## Running tests

```bash
dotnet test --configuration Release
```

Tests cover the price anomaly detector, booking orchestration, mock provider, domain model validation, and API integration tests via `WebApplicationFactory`. No Azure credentials needed.

---

## What's new in v2.0.0

- Multi-provider support — OpenAI, Anthropic (Claude), Ollama, Azure OpenAI
- Mock provider — works with zero credentials, perfect for local development
- Fluent configuration API — `options.UseOpenAI(...)` replaces config sections
- Multi-service architecture — API, SearchWorker, AiWorker connected via RabbitMQ
- Serilog centralized logging across all services
- Docker Compose for running the full system locally
- All existing interfaces unchanged — drop-in upgrade from v1.0.0

---

## Tech stack

`.NET 10` · `OpenAI` · `Anthropic` · `Ollama` · `Azure OpenAI` · `Azure AI Search` · `RabbitMQ` · `MassTransit` · `Serilog` · `AKS` · `Docker` · `GitHub Actions` · `xUnit` · `FluentAssertions`

---

## License

MIT