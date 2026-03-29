namespace TravelAI.Core.Models;

public sealed record Itinerary
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Destination { get; init; }
    public required DateOnly DepartureDate { get; init; }
    public required DateOnly ReturnDate { get; init; }
    public required IReadOnlyList<ItineraryDay> Days { get; init; }
    public required TravellerProfile Traveller { get; init; }
    public decimal EstimatedCostGbp { get; init; }
    public ItineraryStatus Status { get; init; } = ItineraryStatus.Draft;
    public string? AiSummary { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ItineraryDay
{
    public required int DayNumber { get; init; }
    public required DateOnly Date { get; init; }
    public required IReadOnlyList<Activity> Activities { get; init; }
    public string? Theme { get; init; }
    public string? AiInsight { get; init; }
}

public sealed record Activity
{
    public required string Name { get; init; }
    public required TimeOnly StartTime { get; init; }
    public required TimeOnly EndTime { get; init; }
    public required ActivityType Type { get; init; }
    public string? Location { get; init; }
    public string? Notes { get; init; }
    public decimal? CostGbp { get; init; }
    public double? ConfidenceScore { get; init; }
}

public sealed record TravellerProfile
{
    public required string Name { get; init; }
    public required string Email { get; init; }
    public IReadOnlyList<string> Preferences { get; init; } = [];
    public IReadOnlyList<string> DietaryRequirements { get; init; } = [];
    public TravelTier Tier { get; init; } = TravelTier.Standard;
}

public sealed record FlightOption
{
    public required string Id { get; init; }
    public required string Origin { get; init; }
    public required string Destination { get; init; }
    public required DateTimeOffset Departure { get; init; }
    public required DateTimeOffset Arrival { get; init; }
    public required string Carrier { get; init; }
    public required decimal PriceGbp { get; init; }
    public required CabinClass Cabin { get; init; }
    public bool IsDirectFlight { get; init; }
    public PriceAnomaly? PriceAnomaly { get; init; }
}

public sealed record PriceAnomaly
{
    public required AnomalyType Type { get; init; }
    public required double DeviationPercent { get; init; }
    public required decimal ExpectedPrice { get; init; }
    public required string Explanation { get; init; }
    public double ConfidenceScore { get; init; }
}

public sealed record DestinationSearchResult
{
    public required string DestinationId { get; init; }
    public required string Name { get; init; }
    public required string Country { get; init; }
    public required double SemanticScore { get; init; }
    public required string MatchReason { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public decimal AverageWeeklyPriceGbp { get; init; }
}

public sealed record BookingResult
{
    public required string BookingReference { get; init; }
    public required BookingStatus Status { get; init; }
    public required string ConfirmationMessage { get; init; }
    public DateTimeOffset BookedAt { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<string> AutomationSteps { get; init; } = [];
    public string? FailureReason { get; init; }
}

public enum ItineraryStatus { Draft, Confirmed, Cancelled, Completed }
public enum ActivityType { Flight, Hotel, Dining, Attraction, Transfer, Leisure }
public enum TravelTier { Standard, Premium, Luxury }
public enum CabinClass { Economy, PremiumEconomy, Business, First }
public enum AnomalyType { PriceSurge, UnexpectedDeal, SeasonalDeviation, SupplyConstraint }
public enum BookingStatus { Confirmed, Pending, Failed, PartiallyBooked }
