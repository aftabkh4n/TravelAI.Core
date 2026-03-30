using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TravelAI.Core.Extensions;
using TravelAI.Core.Interfaces;
using TravelAI.Core.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTravelAI(builder.Configuration);
builder.Services.AddTravelAIHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors();
app.UseTravelAIObservability();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthJson
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = h => h.Tags.Contains("ready"),
    ResponseWriter = WriteHealthJson
});

app.MapPost("/api/itinerary/generate", async (
    GenerateItineraryRequest req,
    IItineraryGenerationService generator,
    CancellationToken ct) =>
{
    var traveller = new TravellerProfile
    {
        Name = req.TravellerName,
        Email = req.TravellerEmail,
        Preferences = req.Preferences ?? [],
        DietaryRequirements = req.DietaryRequirements ?? [],
        Tier = req.Tier
    };
    var itinerary = await generator.GenerateAsync(traveller, req.Destination, req.Departure, req.ReturnDate, req.AdditionalInstructions, ct);
    return Results.Ok(itinerary);
})
.WithName("GenerateItinerary")
.WithSummary("Generate an AI-powered travel itinerary")
.Produces<Itinerary>()
.ProducesProblem(429);

app.MapPost("/api/itinerary/refine", async (RefineItineraryRequest req, IItineraryGenerationService generator, CancellationToken ct) =>
    Results.Ok(await generator.RefineAsync(req.Itinerary, req.Feedback, ct)))
.WithName("RefineItinerary")
.WithSummary("Refine an itinerary with natural language feedback")
.Produces<Itinerary>();

app.MapGet("/api/destinations/search", async (
    string q, int maxResults, string? from,
    IDestinationSearchService search, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.BadRequest(new { error = "Query parameter 'q' is required." });
    var results = await search.SearchAsync(q, maxResults, from, cancellationToken: ct);
    return Results.Ok(new { query = q, results, count = results.Count });
})
.WithName("SearchDestinations")
.WithSummary("Semantic destination search — try 'warm Mediterranean with local food'")
.Produces<object>()
.ProducesProblem(400);

app.MapPost("/api/flights/analyse", async (IEnumerable<FlightOption> flights, IPriceAnomalyDetector detector, CancellationToken ct) =>
{
    var results = new List<FlightOption>();
    await foreach (var f in detector.AnalyseBatchAsync(flights, ct))
        results.Add(f);
    return Results.Ok(new { analysed = results.Count, anomaliesDetected = results.Count(f => f.PriceAnomaly is not null), flights = results });
})
.WithName("AnalyseFlightPrices")
.WithSummary("Detect price anomalies in flight search results")
.Produces<object>();

app.MapPost("/api/bookings", async (Itinerary itinerary, IBookingAutomationService bookingService, CancellationToken ct) =>
{
    var result = await bookingService.BookAsync(itinerary, ct);
    return result.Status == BookingStatus.Failed
        ? Results.Problem(result.FailureReason, statusCode: 422)
        : Results.Ok(result);
})
.WithName("BookItinerary")
.WithSummary("Execute automated booking for a confirmed itinerary")
.Produces<BookingResult>()
.ProducesProblem(422);

app.MapDelete("/api/bookings/{reference}", async (string reference, IBookingAutomationService bookingService, CancellationToken ct) =>
    Results.Ok(await bookingService.CancelAsync(reference, ct)))
.WithName("CancelBooking")
.WithSummary("Cancel a booking and initiate refund")
.Produces<BookingResult>();



record GenerateItineraryRequest(
    string TravellerName, string TravellerEmail, string Destination,
    DateOnly Departure, DateOnly ReturnDate,
    TravelTier Tier = TravelTier.Standard,
    IReadOnlyList<string>? Preferences = null,
    IReadOnlyList<string>? DietaryRequirements = null,
    string? AdditionalInstructions = null);

record RefineItineraryRequest(Itinerary Itinerary, string Feedback);

static Task WriteHealthJson(HttpContext ctx, HealthReport report)
{
    ctx.Response.ContentType = "application/json";
    return ctx.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description,
            durationMs = e.Value.Duration.TotalMilliseconds
        })
    });
}


app.Run();