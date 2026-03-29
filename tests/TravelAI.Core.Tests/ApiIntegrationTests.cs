using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using TravelAI.Core.Interfaces;
using TravelAI.Core.Models;
using Xunit;

namespace TravelAI.Core.Tests;

/// <summary>
/// Integration tests for the TravelAI.Api sample using WebApplicationFactory.
/// Replaces Azure service dependencies with in-memory fakes.
/// </summary>
public sealed class ApiIntegrationTests : IClassFixture<TravelAIWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(TravelAIWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GET_HealthLive_Returns200()
    {
        var response = await _client.GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_DestinationSearch_WithQuery_Returns200()
    {
        var response = await _client.GetAsync("/api/destinations/search?q=warm+beach&maxResults=5");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_DestinationSearch_WithoutQuery_Returns400()
    {
        var response = await _client.GetAsync("/api/destinations/search?q=");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_FlightAnalysis_Returns200WithAnomalyData()
    {
        var flights = new[]
        {
            new FlightOption
            {
                Id = "f1", Origin = "LHR", Destination = "JFK",
                Departure = DateTimeOffset.UtcNow.AddDays(30),
                Arrival = DateTimeOffset.UtcNow.AddDays(30).AddHours(8),
                Carrier = "BA", PriceGbp = 145m, Cabin = CabinClass.Economy
            }
        };

        var response = await _client.PostAsJsonAsync("/api/flights/analyse", flights);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FlightAnalysisResponse>();
        body!.Analysed.Should().Be(1);
    }

    [Fact]
    public async Task POST_BookItinerary_NonDraft_Returns422()
    {
        var itinerary = BuildItinerary(ItineraryStatus.Confirmed);
        var response = await _client.PostAsJsonAsync("/api/bookings", itinerary);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static Itinerary BuildItinerary(ItineraryStatus status) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Title = "Test",
        Destination = "Paris",
        DepartureDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
        ReturnDate = DateOnly.FromDateTime(DateTime.Today.AddDays(37)),
        Status = status,
        Days = [],
        Traveller = new TravellerProfile { Name = "Test", Email = "t@t.com" }
    };

    private record FlightAnalysisResponse(int Analysed, int AnomaliesDetected, IReadOnlyList<FlightOption> Flights);
}

/// <summary>Custom factory that swaps Azure service dependencies for test fakes.</summary>
public sealed class TravelAIWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove real Azure service registrations and substitute fakes
            var serviceTypes = new[]
            {
                typeof(IItineraryGenerationService),
                typeof(IDestinationSearchService),
                typeof(IBookingAutomationService),
            };

            foreach (var type in serviceTypes)
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == type);
                if (descriptor is not null) services.Remove(descriptor);
            }

            services.AddScoped<IItineraryGenerationService, FakeItineraryGenerationService>();
            services.AddScoped<IDestinationSearchService, FakeDestinationSearchService>();
            services.AddScoped<IBookingAutomationService, FakeBookingService>();
        });
    }
}

// ── Fakes for integration tests ────────────────────────────────────────────
internal sealed class FakeDestinationSearchService : IDestinationSearchService
{
    public Task<IReadOnlyList<DestinationSearchResult>> SearchAsync(
        string query, int maxResults = 10,
        string? departureAirport = null,
        DateOnly? travelDate = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DestinationSearchResult> results =
        [
            new DestinationSearchResult
            {
                DestinationId = "rome-it",
                Name = "Rome",
                Country = "Italy",
                SemanticScore = 0.95,
                MatchReason = "Strong match on culture, food, history",
                Tags = ["history", "food", "culture"],
                AverageWeeklyPriceGbp = 285m
            }
        ];
        return Task.FromResult(results);
    }
}

internal sealed class FakeBookingService : IBookingAutomationService
{
    public Task<BookingResult> BookAsync(Itinerary itinerary, CancellationToken ct = default)
    {
        if (itinerary.Status != ItineraryStatus.Draft)
            return Task.FromResult(new BookingResult
            {
                BookingReference = string.Empty,
                Status = BookingStatus.Failed,
                ConfirmationMessage = string.Empty,
                FailureReason = "Itinerary is not in Draft status — cannot book."
            });

        return Task.FromResult(new BookingResult
        {
            BookingReference = "TAI-TEST-001",
            Status = BookingStatus.Confirmed,
            ConfirmationMessage = "Test booking confirmed.",
            AutomationSteps = ["Step 1", "Step 2"]
        });
    }

    public Task<BookingResult> CancelAsync(string bookingReference, CancellationToken ct = default)
        => Task.FromResult(new BookingResult
        {
            BookingReference = bookingReference,
            Status = BookingStatus.Confirmed,
            ConfirmationMessage = $"{bookingReference} cancelled."
        });
}
