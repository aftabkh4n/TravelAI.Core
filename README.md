# TravelAI.Core

[![NuGet](https://img.shields.io/nuget/v/TravelAI.Core.svg)](https://www.nuget.org/packages/TravelAI.Core)
[![NuGet Downloads](https://img.shields.io/nuget/dt/TravelAI.Core.svg)](https://www.nuget.org/packages/TravelAI.Core)
[![CI](https://github.com/yourusername/TravelAI.Core/actions/workflows/ci.yml/badge.svg)](https://github.com/yourusername/TravelAI.Core/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com)

A **.NET 10** library for building **AI-native travel platforms**. Wraps Azure OpenAI, Azure AI Search, and Azure ML into clean, testable domain services — ready to drop into any ASP.NET Core application.

---

## Features

| Service | What it does |
|---|---|
| `IItineraryGenerationService` | GPT-4o powered day-by-day itinerary generation with cost estimates |
| `IPriceAnomalyDetector` | Statistical + ML anomaly detection on flight pricing data |
| `IDestinationSearchService` | Semantic + vector search over destinations using Azure AI Search |
| `IBookingAutomationService` | Orchestrated booking workflows with retry and rollback |

---

## Installation

```bash
dotnet add package TravelAI.Core
```

---

## Quick Start

### 1. Register services

```csharp
builder.Services.AddTravelAI(builder.Configuration);
builder.Services.AddTravelAIHealthChecks();
```

### 2. Configure

```json
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
    "PriceAnomaly": { "AnomalyThresholdPercent": 25.0 },
    "DestinationSearch": { "IndexName": "destinations" }
  }
}
```

### 3. Use in your API

```csharp
public class TripController(IItineraryGenerationService generator) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] TripRequest req)
    {
        var traveller = new TravellerProfile
        {
            Name = req.Name,
            Email = req.Email,
            Preferences = ["food", "history", "architecture"],
            Tier = TravelTier.Premium
        };

        var itinerary = await generator.GenerateAsync(
            traveller, "Rome, Italy",
            DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            DateOnly.FromDateTime(DateTime.Today.AddDays(37)));

        return Ok(itinerary);
    }
}
```

---

## Architecture

```
TravelAI.Core
├── Interfaces/          # IItineraryGenerationService, IPriceAnomalyDetector, ...
├── Models/              # Itinerary, FlightOption, PriceAnomaly, BookingResult, ...
├── Services/            # Azure AI-backed implementations
│   ├── ItineraryGenerationService.cs
│   ├── PriceAnomalyDetector.cs
│   ├── DestinationSearchService.cs
│   └── BookingAutomationService.cs
├── Middleware/          # ObservabilityMiddleware, AiRateLimitMiddleware
├── HealthChecks/        # AzureOpenAIHealthCheck, AzureSearchHealthCheck
└── Extensions/          # AddTravelAI(), AddTravelAIHealthChecks(), UseTravelAIObservability()

samples/TravelAI.Api     # Full ASP.NET Core 10 minimal API
tests/TravelAI.Core.Tests # Unit + integration tests (xUnit + FluentAssertions)
deploy/                  # Dockerfile, Kubernetes manifests, GitHub Actions, scripts
```

**Azure services:** Azure OpenAI (GPT-4o) · Azure AI Search · Azure Kubernetes Service

---

## Health Checks

```csharp
// Exposes /health/live and /health/ready
builder.Services.AddTravelAIHealthChecks();
app.MapHealthChecks("/health/live", ...);
app.MapHealthChecks("/health/ready", ...);
```

## Running Tests

```bash
dotnet test --configuration Release
```

## Deploy to AKS

See [`deploy/DEPLOYMENT.md`](deploy/DEPLOYMENT.md) for the full step-by-step guide.

```bash
# Provision all Azure resources
bash deploy/scripts/provision-azure.sh

# Push to main — CI/CD handles the rest
git push origin main
```

---

## Roadmap

- [x] Itinerary generation (GPT-4o)
- [x] Price anomaly detection
- [x] Semantic destination search (Azure AI Search)
- [x] Booking automation with retry & rollback
- [x] Observability middleware + rate limiting
- [x] Azure health checks
- [x] Sample ASP.NET Core 10 minimal API
- [x] AKS deployment with GitHub Actions CI/CD
- [ ] Streaming itinerary generation (SSE)
- [ ] Redis cache layer
- [ ] Hotel availability (Amadeus / Travelport)

---

## Tech Stack

`.NET 10` · `Azure OpenAI` · `Azure AI Search` · `AKS` · `Helm` · `GitHub Actions` · `xUnit` · `FluentAssertions`

## License

MIT
