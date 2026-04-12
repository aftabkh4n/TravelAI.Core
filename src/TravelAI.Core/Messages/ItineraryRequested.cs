namespace TravelAI.Core.Messages;

public record ItineraryRequested(
    Guid     CorrelationId,
    string   TravellerName,
    string   TravellerEmail,
    string   Destination,
    DateOnly Departure,
    DateOnly ReturnDate,
    string?  AdditionalInstructions);