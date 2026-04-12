namespace TravelAI.Core.Messages;

public record SearchRequested(
    Guid   CorrelationId,
    string Query,
    int    MaxResults,
    string? FromLocation,
    string  RequestedBy);