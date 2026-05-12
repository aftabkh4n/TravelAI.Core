using TravelAI.Core.Interfaces;
using TravelAI.Core.Models;

namespace TravelAI.Core.Services;

public sealed class MockDestinationSearchService : IDestinationSearchService
{
    private static readonly IReadOnlyList<DestinationSearchResult> AllDestinations =
    [
        new DestinationSearchResult
        {
            DestinationId = "rome-it",
            Name = "Rome",
            Country = "Italy",
            SemanticScore = 0.97,
            MatchReason = "A timeless city of ancient wonders, world-class cuisine, and vibrant piazzas.",
            Tags = ["history", "culture", "food", "architecture", "art"],
            AverageWeeklyPriceGbp = 850m
        },
        new DestinationSearchResult
        {
            DestinationId = "barcelona-es",
            Name = "Barcelona",
            Country = "Spain",
            SemanticScore = 0.94,
            MatchReason = "Gaudí's modernist masterpieces, golden beaches, and a legendary food scene.",
            Tags = ["beach", "architecture", "food", "nightlife", "art"],
            AverageWeeklyPriceGbp = 720m
        },
        new DestinationSearchResult
        {
            DestinationId = "kyoto-jp",
            Name = "Kyoto",
            Country = "Japan",
            SemanticScore = 0.91,
            MatchReason = "Ancient temples, serene bamboo groves, and the living art of Japanese tradition.",
            Tags = ["temples", "culture", "nature", "history", "zen"],
            AverageWeeklyPriceGbp = 1350m
        },
        new DestinationSearchResult
        {
            DestinationId = "marrakech-ma",
            Name = "Marrakech",
            Country = "Morocco",
            SemanticScore = 0.88,
            MatchReason = "Labyrinthine medinas, fragrant souks, and spectacular desert sunsets.",
            Tags = ["culture", "food", "markets", "desert", "history"],
            AverageWeeklyPriceGbp = 580m
        },
        new DestinationSearchResult
        {
            DestinationId = "lisbon-pt",
            Name = "Lisbon",
            Country = "Portugal",
            SemanticScore = 0.85,
            MatchReason = "Pastel-coloured hilltop neighbourhoods, soulful fado music, and superb seafood.",
            Tags = ["culture", "food", "nightlife", "history", "coastal"],
            AverageWeeklyPriceGbp = 650m
        }
    ];

    public Task<IReadOnlyList<DestinationSearchResult>> SearchAsync(
        string naturalLanguageQuery,
        int maxResults = 10,
        string? departureAirport = null,
        DateOnly? travelDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = naturalLanguageQuery.ToLowerInvariant();
        var results = AllDestinations
            .Select(d => (Destination: d, Score: ComputeScore(d, query)))
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => x.Destination with { SemanticScore = x.Score })
            .ToList();

        return Task.FromResult<IReadOnlyList<DestinationSearchResult>>(results);
    }

    private static double ComputeScore(DestinationSearchResult d, string query)
    {
        var score = d.SemanticScore;
        if (query.Contains(d.Name.ToLowerInvariant()) || query.Contains(d.Country.ToLowerInvariant()))
            score = Math.Min(1.0, score * 1.15);
        foreach (var tag in d.Tags)
            if (query.Contains(tag))
                score = Math.Min(1.0, score * 1.04);
        return score;
    }
}
