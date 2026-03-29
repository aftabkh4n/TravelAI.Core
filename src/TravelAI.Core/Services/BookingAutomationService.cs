using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TravelAI.Core.Interfaces;
using TravelAI.Core.Models;

namespace TravelAI.Core.Services;

public sealed class BookingAutomationService : IBookingAutomationService
{
    private readonly BookingAutomationOptions _options;
    private readonly ILogger<BookingAutomationService> _logger;

    public BookingAutomationService(IOptions<BookingAutomationOptions> options, ILogger<BookingAutomationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<BookingResult> BookAsync(Itinerary itinerary, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting booking for itinerary {Id} — {Destination}", itinerary.Id, itinerary.Destination);

        if (itinerary.Status != ItineraryStatus.Draft)
            return Fail($"Itinerary {itinerary.Id} is not in Draft status — cannot book.");

        var steps = new List<string>();
        string? flightRef = null;

        try
        {
            steps.Add("Locating available flights…");
            var flights = itinerary.Days.SelectMany(d => d.Activities).Where(a => a.Type == ActivityType.Flight).ToList();

            if (flights.Count > 0)
            {
                steps.Add($"Reserving {flights.Count} flight segment(s)…");
                flightRef = await ReserveFlightsAsync(cancellationToken);
                steps.Add($"Flights reserved — ref {flightRef}");
            }

            steps.Add("Locating hotel availability…");
            var hotels = itinerary.Days.SelectMany(d => d.Activities).Where(a => a.Type == ActivityType.Hotel).ToList();

            if (hotels.Count > 0)
            {
                steps.Add($"Reserving {hotels.Count} hotel night(s)…");
                var hotelRef = await ReserveHotelsAsync(cancellationToken);
                steps.Add($"Hotel reserved — ref {hotelRef}");
            }

            steps.Add("Generating personalised confirmation…");
            var nights = itinerary.ReturnDate.DayNumber - itinerary.DepartureDate.DayNumber;
            var message = $"Great news, {itinerary.Traveller.Name}! Your {nights}-night trip to {itinerary.Destination} " +
                          $"({itinerary.DepartureDate:d MMM} – {itinerary.ReturnDate:d MMM yyyy}) is confirmed. " +
                          $"Total: £{itinerary.EstimatedCostGbp:N0}. Safe travels!";

            var reference = GenerateBookingReference();
            steps.Add($"Booking complete — reference {reference}");

            return new BookingResult { BookingReference = reference, Status = BookingStatus.Confirmed, ConfirmationMessage = message, AutomationSteps = steps };
        }
        catch (PartialBookingException ex)
        {
            _logger.LogWarning(ex, "Partial booking for itinerary {Id}", itinerary.Id);
            steps.Add($"Hotel reservation failed: {ex.Message}");
            return new BookingResult
            {
                BookingReference = flightRef ?? GenerateBookingReference(),
                Status = BookingStatus.PartiallyBooked,
                ConfirmationMessage = "Flights reserved but hotel requires manual completion. Our team will contact you within 2 hours.",
                AutomationSteps = steps,
                FailureReason = ex.Message
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Booking failed for itinerary {Id}", itinerary.Id);
            if (flightRef is not null) { steps.Add("Rolling back flight reservation…"); await TryRollbackAsync(flightRef); steps.Add("Rolled back."); }
            return Fail($"Booking failed: {ex.Message}", steps);
        }
    }

    public async Task<BookingResult> CancelAsync(string bookingReference, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cancelling booking {Reference}", bookingReference);
        await Task.Delay(200, cancellationToken);
        return new BookingResult
        {
            BookingReference = bookingReference,
            Status = BookingStatus.Confirmed,
            ConfirmationMessage = $"Booking {bookingReference} cancelled. Refunds processed within 5-7 working days.",
            AutomationSteps = ["Cancellation sent to GDS", "Refund initiated", "Confirmation email queued"]
        };
    }

    private async Task<string> ReserveFlightsAsync(CancellationToken ct)
    {
        await RetryAsync(async () => await Task.Delay(150, ct), _options.MaxRetryAttempts, ct);
        return $"FLT-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    private async Task<string> ReserveHotelsAsync(CancellationToken ct)
    {
        try { await RetryAsync(async () => await Task.Delay(120, ct), _options.MaxRetryAttempts, ct); }
        catch (Exception ex) { throw new PartialBookingException("Hotel inventory unavailable", ex); }
        return $"HTL-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    private async Task TryRollbackAsync(string flightRef)
    {
        try { await Task.Delay(100); _logger.LogInformation("Rolled back {Ref}", flightRef); }
        catch (Exception ex) { _logger.LogError(ex, "Rollback failed for {Ref}", flightRef); }
    }

    private static async Task RetryAsync(Func<Task> action, int maxAttempts, CancellationToken ct, int delayMs = 200)
    {
        for (var i = 1; i <= maxAttempts; i++)
            try { await action(); return; }
            catch when (i < maxAttempts) { await Task.Delay(delayMs * i, ct); }
    }

    private static string GenerateBookingReference() => $"TAI-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

    private static BookingResult Fail(string reason, IReadOnlyList<string>? steps = null) => new()
    {
        BookingReference = string.Empty,
        Status = BookingStatus.Failed,
        ConfirmationMessage = string.Empty,
        FailureReason = reason,
        AutomationSteps = steps ?? []
    };
}

public sealed class BookingAutomationOptions
{
    public const string SectionName = "TravelAI:BookingAutomation";
    public int MaxRetryAttempts { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
}

internal sealed class PartialBookingException(string message, Exception inner) : Exception(message, inner);
