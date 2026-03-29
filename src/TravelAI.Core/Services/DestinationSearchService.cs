using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TravelAI.Core.Interfaces;
using TravelAI.Core.Models;

namespace TravelAI.Core.Services;

public sealed class DestinationSearchService : IDestinationSearchService
{
    private readonly SearchClient _searchClient;
    private readonly DestinationSearchOptions _options;
    private readonly ILogger<DestinationSearchService> _logger;

    public DestinationSearchService(SearchClient searchClient, IOptions<DestinationSearchOptions> options, ILogger<DestinationSearchService> logger)
    {
        _searchClient = searchClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DestinationSearchResult>> SearchAsync(
        string naturalLanguageQuery,
        int maxResults = 10,
        string? departureAirport = null,
        DateOnly? travelDate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Semantic destination search: \"{Query}\"", naturalLanguageQuery);

        var searchOptions = new SearchOptions
        {
            Size = maxResults,
            QueryType = SearchQueryType.Semantic,
            SemanticSearch = new SemanticSearchOptions
            {
                SemanticConfigurationName = _options.SemanticConfigurationName,
                QueryCaption = new QueryCaption(QueryCaptionType.Extractive) { HighlightEnabled = false },
                QueryAnswer = new QueryAnswer(QueryAnswerType.Extractive) { HighlightEnabled = false }
            },
            Select = { "destinationId", "name", "country", "tags", "averageWeeklyPriceGbp", "description" }
        };

        if (departureAirport is not null)
            searchOptions.Filter = $"connectedAirports/any(a: a eq '{departureAirport}')";

        var response = await _searchClient.SearchAsync<SearchDocument>(naturalLanguageQuery, searchOptions, cancellationToken);
        var results = new List<DestinationSearchResult>();

        await foreach (var result in response.Value.GetResultsAsync())
        {
            var doc = result.Document;
            results.Add(new DestinationSearchResult
            {
                DestinationId = doc.GetString("destinationId") ?? string.Empty,
                Name = doc.GetString("name") ?? string.Empty,
                Country = doc.GetString("country") ?? string.Empty,
                SemanticScore = result.SemanticSearch?.RerankerScore ?? result.Score ?? 0,
                MatchReason = result.SemanticSearch?.Captions?.FirstOrDefault()?.Text ?? "Matched on preferences",
                Tags = doc.GetStringArray("tags"),
                AverageWeeklyPriceGbp = doc.GetDecimal("averageWeeklyPriceGbp")
            });
        }

        return results;
    }
}

public sealed class DestinationSearchOptions
{
    public const string SectionName = "TravelAI:DestinationSearch";
    public required string IndexName { get; set; }
    public string SemanticConfigurationName { get; set; } = "travel-semantic-config";
}

internal static class SearchDocumentExtensions
{
    public static string? GetString(this SearchDocument doc, string key) =>
        doc.TryGetValue(key, out var val) ? val?.ToString() : null;
    public static IReadOnlyList<string> GetStringArray(this SearchDocument doc, string key) =>
        doc.TryGetValue(key, out var val) && val is IEnumerable<object> items
            ? items.Select(i => i.ToString() ?? string.Empty).ToList() : [];
    public static decimal GetDecimal(this SearchDocument doc, string key) =>
        doc.TryGetValue(key, out var val) && decimal.TryParse(val?.ToString(), out var d) ? d : 0m;
}
