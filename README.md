# TravelAI.Core

[![CI](https://github.com/aftabkh4n/TravelAI.Core/actions/workflows/ci.yml/badge.svg)](https://github.com/aftabkh4n/TravelAI.Core/actions)
[![Deploy](https://github.com/aftabkh4n/TravelAI.Core/actions/workflows/deploy.yml/badge.svg)](https://github.com/aftabkh4n/TravelAI.Core/actions)
[![NuGet](https://img.shields.io/nuget/v/TravelAI.Core.svg)](https://www.nuget.org/packages/TravelAI.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com)

A .NET 10 library for building AI-native travel platforms. It wraps Azure OpenAI, Azure AI Search, and AKS into clean, testable domain services — drop it into any ASP.NET Core app and you've got intelligent itinerary generation, semantic destination search, and real-time price anomaly detection out of the box.

**Live API** → `http://51.11.45.147` (deployed on Azure Kubernetes Service)

---

## What it does

| Service | Description |
|---|---|
| `IItineraryGenerationService` | Generates day-by-day travel itineraries using GPT-4o, with cost estimates tailored to the traveller's preferences |
| `IPriceAnomalyDetector` | Flags flight price anomalies — surges, unexpected deals, seasonal deviations — using statistical analysis against historical baselines |
| `IDestinationSearchService` | Semantic + vector search over destinations via Azure AI Search. Understands queries like *"warm with beaches and good food, not too touristy"* |
| `IBookingAutomationService` | Orchestrates end-to-end bookings with retry logic, partial booking handling, and automatic rollback on failure |

---

## Installation

```bash
dotnet add package TravelAI.Core
```

---

## Quick start

```csharp
// Program.cs
builder.Services.AddTravelAI(builder.Configuration);
builder.Services.AddTravelAIHealthChecks();
app.UseTravelAIObservability();
```

```json
// appsettings.json
{
  "TravelAI": {
    "AzureOpenAI": {
      "Endpoint": "https://your-resource.openai.azure.com/",
      "ApiKey": "YOUR_KEY"
    },
    "AzureSearch": {
      "Endpoint": "https://your-search.search.windows.net",
      "ApiKey": "YOUR_KEY"
    },
    "ItineraryGeneration": { "DeploymentName": "gpt-4o" },
    "DestinationSearch": { "IndexName": "destinations" }
  }
}
```

```csharp
// Generate a personalised itinerary
var itinerary = await generator.GenerateAsync(
    new TravellerProfile { Name = "Aftab", Email = "a@example.com", Tier = TravelTier.Premium },
    destination: "Rome, Italy",
    departure: DateOnly.Parse("2025-08-01"),
    returnDate: DateOnly.Parse("2025-08-08"),
    additionalInstructions: "Avoid tourist traps, focus on local food");

// Semantic destination search
var results = await search.SearchAsync("warm Mediterranean with history and local food");

// Detect price anomalies in flight results
await foreach (var flight in detector.AnalyseBatchAsync(flights))
{
    if (flight.PriceAnomaly?.Type == AnomalyType.UnexpectedDeal)
        Console.WriteLine($"Great deal: {flight.Origin}→{flight.Destination} at £{flight.PriceGbp}");
}
```

---

## Architecture

```
TravelAI.Core
├── Interfaces/       # Clean contracts for all services
├── Models/           # Domain models — Itinerary, FlightOption, PriceAnomaly, BookingResult
├── Services/         # Azure AI-backed implementations
├── Middleware/       # Observability (correlation IDs, structured logging) + AI rate limiting
├── HealthChecks/     # Azure OpenAI and AI Search health probes
└── Extensions/       # One-line DI registration

samples/TravelAI.Api  # Full ASP.NET Core 10 minimal API
tests/                # xUnit + FluentAssertions — unit and WebApplicationFactory integration tests
deploy/               # Dockerfile, Kubernetes manifests, GitHub Actions CI/CD, provisioning scripts
```

**Azure services used:**
- Azure OpenAI (GPT-4o) — itinerary generation and natural language refinement
- Azure AI Search — semantic + hybrid vector search
- Azure Kubernetes Service — container orchestration with HPA (2–10 replicas)
- Azure Container Registry — image storage

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

The API is deployed to AKS via GitHub Actions. Every push to `main` builds, tests, pushes the Docker image, and deploys — with a manual approval gate before production.

```bash
# Provision all Azure resources from scratch
bash deploy/scripts/provision-azure.sh

# Push to main to trigger the pipeline
git push origin main
```

See [`deploy/DEPLOYMENT.md`](deploy/DEPLOYMENT.md) for the full step-by-step guide.

---

## Running tests

```bash
dotnet test --configuration Release
```

Tests cover the price anomaly detector (including edge cases and cabin class multipliers), booking automation orchestration, domain model validation, and API integration tests via `WebApplicationFactory` with fake Azure service dependencies — no Azure credentials needed.

---

## Tech stack

`.NET 10` · `Azure OpenAI` · `Azure AI Search` · `AKS` · `Helm` · `Docker` · `GitHub Actions` · `xUnit` · `FluentAssertions`

---

## License

MIT