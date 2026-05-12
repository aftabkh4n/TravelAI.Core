using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TravelAI.Core.Extensions;
using TravelAI.Core.Interfaces;
using TravelAI.Core.Models;
using TravelAI.Core.Providers;
using TravelAI.Core.Services;
using Xunit;

namespace TravelAI.Core.Tests;

public sealed class MockProviderTests
{
    private static readonly TravellerProfile TestTraveller = new()
    {
        Name = "Jane Doe",
        Email = "jane@example.com",
        Tier = TravelTier.Standard
    };

    private static readonly DateOnly Departure = new(2025, 9, 1);
    private static readonly DateOnly Return = new(2025, 9, 4);

    [Fact]
    public async Task MockItinerary_ReturnsValidItinerary()
    {
        var provider = new MockProvider();
        var service = new ItineraryGenerationService(provider, NullLogger<ItineraryGenerationService>.Instance);

        var itinerary = await service.GenerateAsync(TestTraveller, "Rome", Departure, Return);

        itinerary.Should().NotBeNull();
        itinerary.Days.Should().HaveCount(3);
        itinerary.Days.Should().AllSatisfy(d => d.Activities.Should().HaveCount(3));
        itinerary.EstimatedCostGbp.Should().BeGreaterThan(0);
        itinerary.Title.Should().NotBeNullOrWhiteSpace();
        itinerary.AiSummary.Should().NotBeNullOrWhiteSpace();
        itinerary.Status.Should().Be(ItineraryStatus.Draft);
        itinerary.Destination.Should().Be("Rome");
    }

    [Fact]
    public async Task MockSearch_ReturnsFiveResults()
    {
        var service = new MockDestinationSearchService();

        var results = await service.SearchAsync("European city break culture food");

        results.Should().HaveCount(5);
        results.Should().AllSatisfy(r =>
        {
            r.DestinationId.Should().NotBeNullOrWhiteSpace();
            r.Name.Should().NotBeNullOrWhiteSpace();
            r.SemanticScore.Should().BeInRange(0, 1.1);
        });
    }

    [Fact]
    public async Task MockSearch_RespectsMaxResults()
    {
        var service = new MockDestinationSearchService();

        var results = await service.SearchAsync("any query", maxResults: 3);

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task MockBooking_ReturnsConfirmedWithMockReference()
    {
        var service = new MockBookingAutomationService();
        var itinerary = BuildDraftItinerary();

        var result = await service.BookAsync(itinerary);

        result.Status.Should().Be(BookingStatus.Confirmed);
        result.BookingReference.Should().Be("TAI-MOCK-001");
        result.AutomationSteps.Should().NotBeEmpty();
    }

    [Fact]
    public async Task MockBooking_CancelReturnsConfirmed()
    {
        var service = new MockBookingAutomationService();

        var result = await service.CancelAsync("TAI-MOCK-001");

        result.Status.Should().Be(BookingStatus.Confirmed);
        result.BookingReference.Should().Be("TAI-MOCK-001");
    }

    [Fact]
    public void UseMock_RegistersMockProviderInDI()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddTravelAI(o => o.UseMock());
        var sp = services.BuildServiceProvider();

        var provider = sp.GetRequiredService<ILlmProvider>();
        var searchService = sp.GetRequiredService<IDestinationSearchService>();
        var bookingService = sp.GetRequiredService<IBookingAutomationService>();

        provider.Should().BeOfType<MockProvider>();
        searchService.Should().BeOfType<MockDestinationSearchService>();
        bookingService.Should().BeOfType<MockBookingAutomationService>();
    }

    private static Itinerary BuildDraftItinerary() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Title = "Rome Adventure",
        Destination = "Rome",
        DepartureDate = Departure,
        ReturnDate = Return,
        Status = ItineraryStatus.Draft,
        EstimatedCostGbp = 850m,
        Traveller = TestTraveller,
        Days = []
    };
}
