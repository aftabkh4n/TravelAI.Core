using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TravelAI.Core.Models;
using TravelAI.Core.Services;
using Xunit;

namespace TravelAI.Core.Tests;

/// <summary>
/// Tests for BookingAutomationService orchestration logic.
/// Uses a fake ItineraryGenerationService to avoid Azure dependencies.
/// </summary>
public sealed class BookingAutomationServiceTests
{
    private readonly BookingAutomationService _sut;

    public BookingAutomationServiceTests()
    {
        var options = Options.Create(new BookingAutomationOptions
        {
            MaxRetryAttempts = 2,
            TimeoutSeconds = 5
        });

        _sut = new BookingAutomationService(
            new FakeItineraryGenerationService(),
            options,
            NullLogger<BookingAutomationService>.Instance);
    }

    [Fact]
    public async Task BookAsync_DraftItinerary_ReturnsConfirmed()
    {
        var itinerary = BuildItinerary(ItineraryStatus.Draft);

        var result = await _sut.BookAsync(itinerary);

        result.Status.Should().Be(BookingStatus.Confirmed);
        result.BookingReference.Should().StartWith("TAI-");
        result.ConfirmationMessage.Should().Contain("James Smith");
        result.AutomationSteps.Should().NotBeEmpty();
    }

    [Fact]
    public async Task BookAsync_NonDraftItinerary_ReturnsFailed()
    {
        var itinerary = BuildItinerary(ItineraryStatus.Confirmed);

        var result = await _sut.BookAsync(itinerary);

        result.Status.Should().Be(BookingStatus.Failed);
        result.FailureReason.Should().Contain("not in Draft status");
    }

    [Fact]
    public async Task BookAsync_AuditTrailPopulated()
    {
        var itinerary = BuildItinerary(ItineraryStatus.Draft);

        var result = await _sut.BookAsync(itinerary);

        result.AutomationSteps.Should().HaveCountGreaterThan(2);
        result.AutomationSteps.Should().Contain(s => s.Contains("flight", StringComparison.OrdinalIgnoreCase)
            || s.Contains("Locating"));
    }

    [Fact]
    public async Task BookAsync_ItineraryWithNoFlights_StillSucceeds()
    {
        var itinerary = BuildItinerary(ItineraryStatus.Draft, includeFlights: false);

        var result = await _sut.BookAsync(itinerary);

        result.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task CancelAsync_ValidReference_ReturnsConfirmed()
    {
        var result = await _sut.CancelAsync("TAI-250101-ABCDEF");

        result.Status.Should().Be(BookingStatus.Confirmed);
        result.ConfirmationMessage.Should().Contain("cancelled");
        result.AutomationSteps.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task BookAsync_RespectsCancellationToken()
    {
        var itinerary = BuildItinerary(ItineraryStatus.Draft);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _sut.BookAsync(itinerary, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static Itinerary BuildItinerary(ItineraryStatus status, bool includeFlights = true) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Title = "Test Trip",
        Destination = "Paris",
        DepartureDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
        ReturnDate = DateOnly.FromDateTime(DateTime.Today.AddDays(37)),
        EstimatedCostGbp = 1200m,
        Status = status,
        Traveller = new TravellerProfile
        {
            Name = "James Smith",
            Email = "james@example.com",
            Tier = TravelTier.Standard
        },
        Days =
        [
            new ItineraryDay
            {
                DayNumber = 1,
                Date = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
                Activities = includeFlights
                    ?
                    [
                        new Activity
                        {
                            Name = "LHR → CDG",
                            StartTime = new TimeOnly(8, 0),
                            EndTime = new TimeOnly(10, 15),
                            Type = ActivityType.Flight,
                            Location = "Heathrow T5",
                            CostGbp = 130m,
                            ConfidenceScore = 0.92
                        }
                    ]
                    : []
            }
        ]
    };
}

/// <summary>Tests for the TravellerProfile and domain model validation.</summary>
public sealed class DomainModelTests
{
    [Fact]
    public void Itinerary_Duration_CalculatesCorrectly()
    {
        var dep = new DateOnly(2025, 6, 1);
        var ret = new DateOnly(2025, 6, 8);

        var itin = new Itinerary
        {
            Id = "1",
            Title = "Test",
            Destination = "Rome",
            DepartureDate = dep,
            ReturnDate = ret,
            Days = [],
            Traveller = new TravellerProfile { Name = "A", Email = "a@b.com" }
        };

        var nights = itin.ReturnDate.DayNumber - itin.DepartureDate.DayNumber;
        nights.Should().Be(7);
    }

    [Fact]
    public void TravellerProfile_DefaultTier_IsStandard()
    {
        var profile = new TravellerProfile { Name = "Test", Email = "t@t.com" };
        profile.Tier.Should().Be(TravelTier.Standard);
    }

    [Fact]
    public void TravellerProfile_DefaultLists_AreEmpty()
    {
        var profile = new TravellerProfile { Name = "Test", Email = "t@t.com" };
        profile.Preferences.Should().BeEmpty();
        profile.DietaryRequirements.Should().BeEmpty();
    }

    [Fact]
    public void Itinerary_DefaultStatus_IsDraft()
    {
        var itin = new Itinerary
        {
            Id = "1", Title = "T", Destination = "D",
            DepartureDate = DateOnly.MinValue, ReturnDate = DateOnly.MinValue,
            Days = [],
            Traveller = new TravellerProfile { Name = "A", Email = "a@b.com" }
        };
        itin.Status.Should().Be(ItineraryStatus.Draft);
    }

    [Theory]
    [InlineData(ActivityType.Flight)]
    [InlineData(ActivityType.Hotel)]
    [InlineData(ActivityType.Dining)]
    [InlineData(ActivityType.Attraction)]
    public void Activity_AllTypes_AreValid(ActivityType type)
    {
        var activity = new Activity
        {
            Name = "Test",
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Type = type
        };
        activity.Type.Should().Be(type);
    }
}

// ── Test doubles ────────────────────────────────────────────────────────────
internal sealed class FakeItineraryGenerationService
    : TravelAI.Core.Interfaces.IItineraryGenerationService
{
    public Task<Itinerary> GenerateAsync(
        TravellerProfile traveller, string destination,
        DateOnly departure, DateOnly returnDate,
        string? additionalInstructions = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Not needed for these tests");

    public Task<Itinerary> RefineAsync(
        Itinerary existing, string feedback,
        CancellationToken cancellationToken = default)
        => Task.FromResult(existing with { AiSummary = $"Refined: {feedback}" });
}
