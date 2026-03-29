using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TravelAI.Core.Models;
using TravelAI.Core.Services;
using Xunit;

namespace TravelAI.Core.Tests;

public sealed class PriceAnomalyDetectorTests
{
    private readonly PriceAnomalyDetector _sut;

    public PriceAnomalyDetectorTests()
    {
        var options = Options.Create(new PriceAnomalyOptions { AnomalyThresholdPercent = 25.0 });
        _sut = new PriceAnomalyDetector(options, NullLogger<PriceAnomalyDetector>.Instance);
    }

    [Fact]
    public async Task AnalyseAsync_NormalPrice_ReturnsNoAnomaly()
    {
        var flight = BuildFlight("LHR", "CDG", 90m); // Baseline is £85 — within 25%

        var result = await _sut.AnalyseAsync(flight);

        result.PriceAnomaly.Should().BeNull();
    }

    [Fact]
    public async Task AnalyseAsync_PriceSurge_DetectsAnomaly()
    {
        var flight = BuildFlight("LHR", "JFK", 750m); // Baseline is £380 — ~97% above

        var result = await _sut.AnalyseAsync(flight);

        result.PriceAnomaly.Should().NotBeNull();
        result.PriceAnomaly!.DeviationPercent.Should().BeGreaterThan(50);
        result.PriceAnomaly.ExpectedPrice.Should().Be(380m);
    }

    [Fact]
    public async Task AnalyseAsync_UnexpectedDeal_DetectsAnomaly()
    {
        var flight = BuildFlight("LHR", "DXB", 150m); // Baseline is £320 — very cheap

        var result = await _sut.AnalyseAsync(flight);

        result.PriceAnomaly.Should().NotBeNull();
        result.PriceAnomaly!.Type.Should().Be(AnomalyType.UnexpectedDeal);
        result.PriceAnomaly.DeviationPercent.Should().BeLessThan(0);
    }

    [Fact]
    public async Task AnalyseAsync_UnknownRoute_ReturnsNoAnomaly()
    {
        var flight = BuildFlight("BHX", "NRT", 900m); // No baseline for this route

        var result = await _sut.AnalyseAsync(flight);

        result.PriceAnomaly.Should().BeNull();
    }

    [Fact]
    public async Task AnalyseBatchAsync_MultipleFlights_ProcessesAll()
    {
        var flights = new[]
        {
            BuildFlight("LHR", "CDG", 85m),    // normal
            BuildFlight("LHR", "JFK", 900m),   // surge
            BuildFlight("LHR", "BCN", 30m),    // deal
        };

        var results = new List<FlightOption>();
        await foreach (var f in _sut.AnalyseBatchAsync(flights))
            results.Add(f);

        results.Should().HaveCount(3);
        results[0].PriceAnomaly.Should().BeNull();
        results[1].PriceAnomaly!.Type.Should().NotBe(AnomalyType.UnexpectedDeal);
        results[2].PriceAnomaly!.Type.Should().Be(AnomalyType.UnexpectedDeal);
    }

    [Theory]
    [InlineData(CabinClass.Economy, 380, null)]         // baseline exact — no anomaly
    [InlineData(CabinClass.Business, 380, AnomalyType.UnexpectedDeal)]  // business baseline is 3.5x, so £380 is a deal
    [InlineData(CabinClass.First, 380, AnomalyType.UnexpectedDeal)]     // first baseline is 6x
    public async Task AnalyseAsync_CabinMultiplierApplied(CabinClass cabin, decimal price, AnomalyType? expectedType)
    {
        var flight = BuildFlight("LHR", "JFK", price, cabin);

        var result = await _sut.AnalyseAsync(flight);

        if (expectedType is null)
            result.PriceAnomaly.Should().BeNull();
        else
            result.PriceAnomaly!.Type.Should().Be(expectedType.Value);
    }

    private static FlightOption BuildFlight(
        string origin,
        string destination,
        decimal price,
        CabinClass cabin = CabinClass.Economy) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Origin = origin,
        Destination = destination,
        Departure = DateTimeOffset.UtcNow.AddDays(30),
        Arrival = DateTimeOffset.UtcNow.AddDays(30).AddHours(2),
        Carrier = "BA",
        PriceGbp = price,
        Cabin = cabin
    };
}
