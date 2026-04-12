using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using TravelAI.Core.Extensions;
using TravelAI.Core.Interfaces;
using TravelAI.Core.Messages;
using TravelAI.Core.Models;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .WriteTo.Console(outputTemplate:
           "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"));

// TravelAI core services
builder.Services.AddTravelAI(builder.Configuration);
builder.Services.AddTravelAIHealthChecks();

// MassTransit — publishes messages to workers
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(ctx);
    });
});

// OpenTelemetry tracing
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService("TravelAI.Api"))
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());

builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors();
app.UseSerilogRequestLogging();
app.UseTravelAIObservability();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapHealthChecks("/health/live",  new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteHealthJson });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = h => h.Tags.Contains("ready"), ResponseWriter = WriteHealthJson });

// Search — now publishes to SearchWorker via RabbitMQ
app.MapGet("/api/destinations/search", async (
    string q,
    int maxResults,
    string? from,
    IPublishEndpoint bus,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.BadRequest(new { error = "Query parameter 'q' is required." });

    var correlationId = Guid.NewGuid();

    logger.LogInformation(
        "Publishing search request {CorrelationId} for: {Query}",
        correlationId, q);

    await bus.Publish(new SearchRequested(
        correlationId, q, maxResults, from, "api"), ct);

    return Results.Accepted($"/api/search/status/{correlationId}", new
    {
        correlationId,
        message = "Search request queued. Results will be available shortly.",
        statusUrl = $"/api/search/status/{correlationId}"
    });
})
.WithName("SearchDestinations")
.WithSummary("Semantic destination search via message queue");

// Itinerary generation — publishes to AiWorker via RabbitMQ
app.MapPost("/api/itinerary/generate", async (
    GenerateItineraryRequest req,
    IPublishEndpoint bus,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var correlationId = Guid.NewGuid();

    logger.LogInformation(
        "Publishing itinerary request {CorrelationId} for {Traveller} to {Destination}",
        correlationId, req.TravellerName, req.Destination);

    await bus.Publish(new ItineraryRequested(
        correlationId,
        req.TravellerName,
        req.TravellerEmail,
        req.Destination,
        req.Departure,
        req.ReturnDate,
        req.AdditionalInstructions), ct);

    return Results.Accepted($"/api/itinerary/status/{correlationId}", new
    {
        correlationId,
        message = "Itinerary generation queued. This takes 10-30 seconds.",
        statusUrl = $"/api/itinerary/status/{correlationId}"
    });
})
.WithName("GenerateItinerary")
.WithSummary("Queue an AI-powered itinerary generation request");

// Keep direct endpoints for operations that need immediate response
app.MapPost("/api/flights/analyse", async (
    IEnumerable<FlightOption> flights,
    IPriceAnomalyDetector detector,
    CancellationToken ct) =>
{
    var results = new List<FlightOption>();
    await foreach (var f in detector.AnalyseBatchAsync(flights, ct))
        results.Add(f);
    return Results.Ok(new
    {
        analysed          = results.Count,
        anomaliesDetected = results.Count(f => f.PriceAnomaly is not null),
        flights           = results
    });
})
.WithName("AnalyseFlightPrices")
.WithSummary("Detect price anomalies — runs synchronously");

app.MapPost("/api/itinerary/refine", async (
    RefineItineraryRequest req,
    IItineraryGenerationService generator,
    CancellationToken ct) =>
    Results.Ok(await generator.RefineAsync(req.Itinerary, req.Feedback, ct)))
.WithName("RefineItinerary")
.WithSummary("Refine an itinerary synchronously");

app.MapPost("/api/bookings", async (
    Itinerary itinerary,
    IBookingAutomationService bookingService,
    CancellationToken ct) =>
{
    var result = await bookingService.BookAsync(itinerary, ct);
    return result.Status == BookingStatus.Failed
        ? Results.Problem(result.FailureReason, statusCode: 422)
        : Results.Ok(result);
})
.WithName("BookItinerary")
.WithSummary("Execute automated booking");

app.MapDelete("/api/bookings/{reference}", async (
    string reference,
    IBookingAutomationService bookingService,
    CancellationToken ct) =>
    Results.Ok(await bookingService.CancelAsync(reference, ct)))
.WithName("CancelBooking")
.WithSummary("Cancel a booking");

app.Run();

static Task WriteHealthJson(HttpContext ctx, HealthReport report)
{
    ctx.Response.ContentType = "application/json";
    return ctx.Response.WriteAsJsonAsync(new
    {
        status         = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        checks         = report.Entries.Select(e => new
        {
            name        = e.Key,
            status      = e.Value.Status.ToString(),
            description = e.Value.Description,
            durationMs  = e.Value.Duration.TotalMilliseconds
        })
    });
}

record GenerateItineraryRequest(
    string   TravellerName,
    string   TravellerEmail,
    string   Destination,
    DateOnly Departure,
    DateOnly ReturnDate,
    TravelTier Tier = TravelTier.Standard,
    IReadOnlyList<string>? Preferences           = null,
    IReadOnlyList<string>? DietaryRequirements   = null,
    string?  AdditionalInstructions              = null);

record RefineItineraryRequest(Itinerary Itinerary, string Feedback);