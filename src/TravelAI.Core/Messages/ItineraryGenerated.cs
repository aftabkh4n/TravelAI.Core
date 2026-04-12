namespace TravelAI.Core.Messages;

public record ItineraryGenerated(
    Guid    CorrelationId,
    object  Itinerary,
    bool    Success,
    string? ErrorMessage);