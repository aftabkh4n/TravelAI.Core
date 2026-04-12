using MassTransit;
using TravelAI.Core.Interfaces;
using TravelAI.Core.Messages;

namespace TravelAI.SearchWorker;

public class SearchRequestedConsumer(
    IDestinationSearchService searchService,
    ILogger<SearchRequestedConsumer> logger) : IConsumer<SearchRequested>
{
    public async Task Consume(ConsumeContext<SearchRequested> context)
    {
        var msg = context.Message;

        logger.LogInformation(
            "Processing search request {CorrelationId} for query: {Query}",
            msg.CorrelationId, msg.Query);

        var started = DateTime.UtcNow;

        try
        {
            var results = await searchService.SearchAsync(
                msg.Query,
                msg.MaxResults,
                msg.FromLocation,
                cancellationToken: context.CancellationToken);

            var duration = DateTime.UtcNow - started;

            await context.Publish(new SearchCompleted(
                msg.CorrelationId,
                msg.Query,
                results.Cast<object>().ToList(),
                results.Count,
                duration));

            logger.LogInformation(
                "Search {CorrelationId} completed in {Duration}ms — {Count} results",
                msg.CorrelationId, duration.TotalMilliseconds, results.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Search {CorrelationId} failed for query: {Query}",
                msg.CorrelationId, msg.Query);
            throw;
        }
    }
}