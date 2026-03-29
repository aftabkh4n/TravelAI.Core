using TravelAI.Core.Models;

namespace TravelAI.Core.Interfaces;

public interface IItineraryGenerationService
{
    Task<Itinerary> GenerateAsync(
        TravellerProfile traveller,
        string destination,
        DateOnly departure,
        DateOnly returnDate,
        string? additionalInstructions = null,
        CancellationToken cancellationToken = default);

    Task<Itinerary> RefineAsync(
        Itinerary existing,
        string feedback,
        CancellationToken cancellationToken = default);
}

public interface IPriceAnomalyDetector
{
    Task<FlightOption> AnalyseAsync(
        FlightOption flight,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<FlightOption> AnalyseBatchAsync(
        IEnumerable<FlightOption> flights,
        CancellationToken cancellationToken = default);
}

public interface IDestinationSearchService
{
    Task<IReadOnlyList<DestinationSearchResult>> SearchAsync(
        string naturalLanguageQuery,
        int maxResults = 10,
        string? departureAirport = null,
        DateOnly? travelDate = null,
        CancellationToken cancellationToken = default);
}

public interface IBookingAutomationService
{
    Task<BookingResult> BookAsync(
        Itinerary itinerary,
        CancellationToken cancellationToken = default);

    Task<BookingResult> CancelAsync(
        string bookingReference,
        CancellationToken cancellationToken = default);
}
