using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TravelAI.Core.Interfaces;
using TravelAI.Core.Models;

namespace TravelAI.Core.Services;

public sealed class PriceAnomalyDetector : IPriceAnomalyDetector
{
    private readonly PriceAnomalyOptions _options;
    private readonly ILogger<PriceAnomalyDetector> _logger;

    private static readonly Dictionary<string, decimal> _routeBaselines = new()
    {
        ["LHR-CDG"] = 85m,  ["LHR-JFK"] = 380m, ["LHR-DXB"] = 320m,
        ["LHR-BCN"] = 95m,  ["LHR-LAX"] = 520m, ["LHR-SIN"] = 680m,
        ["MAN-AMS"] = 75m,  ["LGW-PMI"] = 65m,
    };

    public PriceAnomalyDetector(IOptions<PriceAnomalyOptions> options, ILogger<PriceAnomalyDetector> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FlightOption> AnalyseAsync(FlightOption flight, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        var routeKey = $"{flight.Origin}-{flight.Destination}";
        if (!_routeBaselines.TryGetValue(routeKey, out var baseline)) return flight;

        var cabinMultiplier = flight.Cabin switch
        {
            CabinClass.PremiumEconomy => 1.8m,
            CabinClass.Business => 3.5m,
            CabinClass.First => 6.0m,
            _ => 1.0m
        };

        var adjustedBaseline = baseline * cabinMultiplier;
        var deviation = (double)((flight.PriceGbp - adjustedBaseline) / adjustedBaseline) * 100;

        if (Math.Abs(deviation) < _options.AnomalyThresholdPercent) return flight;

        var type = deviation < 0 ? AnomalyType.UnexpectedDeal : DetermineHighType(deviation, flight);
        var anomaly = new PriceAnomaly
        {
            Type = type,
            DeviationPercent = deviation,
            ExpectedPrice = adjustedBaseline,
            ConfidenceScore = Math.Min(1.0, Math.Abs(deviation) / 100.0),
            Explanation = BuildExplanation(type, deviation, adjustedBaseline, flight)
        };

        _logger.LogInformation("Anomaly detected on {Route}: {Type} ({Deviation:F1}%)", routeKey, type, deviation);
        return flight with { PriceAnomaly = anomaly };
    }

    public async IAsyncEnumerable<FlightOption> AnalyseBatchAsync(
        IEnumerable<FlightOption> flights,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var flight in flights)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await AnalyseAsync(flight, cancellationToken);
        }
    }

    private static AnomalyType DetermineHighType(double deviation, FlightOption flight)
    {
        var isPeak = flight.Departure.Month is 7 or 8 or 12;
        return (isPeak, deviation > 80) switch
        {
            (true, _) => AnomalyType.SeasonalDeviation,
            (_, true) => AnomalyType.SupplyConstraint,
            _ => AnomalyType.PriceSurge
        };
    }

    private static string BuildExplanation(AnomalyType type, double deviation, decimal expected, FlightOption flight) => type switch
    {
        AnomalyType.UnexpectedDeal => $"This flight is {Math.Abs(deviation):F0}% below the expected £{expected:F0}. Book soon.",
        AnomalyType.PriceSurge => $"Price is {deviation:F0}% above the typical £{expected:F0}. Consider flexible dates.",
        AnomalyType.SeasonalDeviation => $"Peak season pricing: {deviation:F0}% above the off-peak baseline of £{expected:F0}.",
        AnomalyType.SupplyConstraint => $"Limited availability is driving prices {deviation:F0}% above the route average of £{expected:F0}.",
        _ => $"Unusual pricing: {deviation:F1}% deviation from expected £{expected:F0}."
    };
}

public sealed class PriceAnomalyOptions
{
    public const string SectionName = "TravelAI:PriceAnomaly";
    public double AnomalyThresholdPercent { get; set; } = 25.0;
}
