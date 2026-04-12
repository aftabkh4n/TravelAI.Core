namespace TravelAI.Core.Messages;

public record SearchCompleted(
    Guid         CorrelationId,
    string       Query,
    List<object> Results,
    int          Count,
    TimeSpan     Duration);