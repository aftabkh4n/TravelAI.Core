# TravelAI.Core

[![CI](https://github.com/aftabkh4n/TravelAI.Core/actions/workflows/ci.yml/badge.svg)](https://github.com/aftabkh4n/TravelAI.Core/actions)
[![Deploy](https://github.com/aftabkh4n/TravelAI.Core/actions/workflows/deploy.yml/badge.svg)](https://github.com/aftabkh4n/TravelAI.Core/actions)
[![NuGet](https://img.shields.io/nuget/v/TravelAI.Core.svg)](https://www.nuget.org/packages/TravelAI.Core)
[![NuGet Downloads](https://img.shields.io/nuget/dt/TravelAI.Core.svg)](https://www.nuget.org/packages/TravelAI.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com)

A .NET 10 library for building AI-native travel platforms. Supports OpenAI, Anthropic, Ollama, and Azure OpenAI out of the box - swap providers without changing any application code. Deployed to Azure Kubernetes Service with GitHub Actions CI/CD.

---

## Installation

```bash
dotnet add package TravelAI.Core
```

---

## Quick start

### Zero credentials - try it instantly

```csharp
builder.Services.AddTravelAI(options => options.UseMock());
```

No API keys, no Azure account needed. Returns realistic itineraries, destinations, and booking results straight away. Good for local development and testing before committing to a provider.

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

### Ollama - free and local

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
| `IPriceAnomalyDetector` | Flags flight price anomalies - surges, unexpected deals, seasonal deviations - using statistical analysis against historical baselines |
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

## Architecture

```
TravelAI.Core
├── Interfaces/       # IItineraryGenerationService, IPriceAnomalyDetector, ...
├── Models/           # Itinerary, FlightOption, PriceAnomaly, BookingResult, ...
├── Providers/        # ILlmProvider - OpenAI, Anthropic, Ollama, AzureOpenAI, Mock
├── Services/         # Provider-agnostic service implementations
├── Middleware/       # Observability (correlation IDs, structured logging) + rate limiting
├── HealthChecks/     # Azure OpenAI and AI Search health probes
└── Extensions/       # AddTravelAI() DI registration with fluent provider config

samples/TravelAI.Api  # Full ASP.NET Core 10 minimal API
tests/                # xUnit + FluentAssertions - unit and integration tests
deploy/               # Dockerfile, Kubernetes manifests, GitHub Actions CI/CD
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

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/itinerary/generate` | Generate a personalised itinerary |
| `POST` | `/api/itinerary/refine` | Refine an itinerary with natural language feedback |
| `GET` | `/api/destinations/search?q=...` | Semantic destination search |
| `POST` | `/api/flights/analyse` | Detect price anomalies in flight results |
| `POST` | `/api/bookings` | Execute automated booking |
| `DELETE` | `/api/bookings/{reference}` | Cancel a booking |
| `GET` | `/health/live` | Liveness probe |
| `GET` | `/health/ready` | Readiness probe (checks Azure OpenAI + Search) |

---

## Deployment

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

Tests cover the price anomaly detector, booking orchestration, mock provider, domain model validation, and API integration tests via `WebApplicationFactory`. No Azure credentials needed - the mock provider and test doubles handle everything.

---

## What's new in v2.0.0

- Multi-provider support - OpenAI, Anthropic (Claude), Ollama, Azure OpenAI
- Mock provider - works with zero credentials, perfect for local development
- Fluent configuration API - `options.UseOpenAI(...)` replaces config sections
- Provider-agnostic destination search with mock search included
- All existing interfaces unchanged - drop-in upgrade from v1.0.0

---

## Tech stack

`.NET 10` · `OpenAI` · `Anthropic` · `Ollama` · `Azure OpenAI` · `Azure AI Search` · `AKS` · `Docker` · `GitHub Actions` · `xUnit` · `FluentAssertions`

---

## License

MIT