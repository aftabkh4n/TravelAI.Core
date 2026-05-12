using TravelAI.Core.Interfaces;
using TravelAI.Core.Models;

namespace TravelAI.Core.Providers;

public sealed class MockProvider : ILlmProvider
{
    private const string MockItineraryJson = """
        {
            "title": "Rome: A 3-Day Cultural Journey",
            "aiSummary": "An immersive 3-day exploration of Rome's ancient history, world-class cuisine, and vibrant street life, tailored for the discerning UK traveller.",
            "estimatedCostGbp": 850.00,
            "days": [
                {
                    "dayNumber": 1,
                    "theme": "Ancient Rome",
                    "aiInsight": "Visit the Colosseum first thing in the morning to beat the crowds and experience its grandeur in golden light.",
                    "activities": [
                        {
                            "name": "Colosseum & Roman Forum",
                            "startTime": "09:00",
                            "endTime": "12:30",
                            "type": "Attraction",
                            "location": "Piazza del Colosseo, Rome",
                            "notes": "Book combined ticket online to skip queues. Audio guide recommended.",
                            "costGbp": 45.00,
                            "confidenceScore": 0.96
                        },
                        {
                            "name": "Lunch at Trattoria Polese",
                            "startTime": "13:00",
                            "endTime": "14:30",
                            "type": "Dining",
                            "location": "Campo de' Fiori 40, Rome",
                            "notes": "Authentic Roman trattoria — try the cacio e pepe and tiramisu.",
                            "costGbp": 28.00,
                            "confidenceScore": 0.91
                        },
                        {
                            "name": "Palatine Hill & Circus Maximus",
                            "startTime": "15:00",
                            "endTime": "17:30",
                            "type": "Attraction",
                            "location": "Via Sacra, Rome",
                            "notes": "Included with Colosseum ticket. Stunning views of the Forum.",
                            "costGbp": 0.00,
                            "confidenceScore": 0.94
                        }
                    ]
                },
                {
                    "dayNumber": 2,
                    "theme": "Vatican & Renaissance Art",
                    "aiInsight": "Book Vatican Museums 2 months ahead. The Sistine Chapel is most peaceful right before closing.",
                    "activities": [
                        {
                            "name": "Vatican Museums & Sistine Chapel",
                            "startTime": "08:30",
                            "endTime": "12:00",
                            "type": "Attraction",
                            "location": "Viale Vaticano, Vatican City",
                            "notes": "Pre-booked guided tour included. Dress modestly — shoulders and knees covered.",
                            "costGbp": 55.00,
                            "confidenceScore": 0.97
                        },
                        {
                            "name": "St Peter's Basilica",
                            "startTime": "12:30",
                            "endTime": "14:00",
                            "type": "Attraction",
                            "location": "Piazza San Pietro, Vatican City",
                            "notes": "Free entry. Climb the dome for panoramic views (additional charge).",
                            "costGbp": 8.00,
                            "confidenceScore": 0.95
                        },
                        {
                            "name": "Dinner at Il Sorpasso",
                            "startTime": "19:30",
                            "endTime": "21:30",
                            "type": "Dining",
                            "location": "Via Properzio 31, Prati, Rome",
                            "notes": "Beloved local bistro near Vatican. Reserve a table in advance.",
                            "costGbp": 38.00,
                            "confidenceScore": 0.88
                        }
                    ]
                },
                {
                    "dayNumber": 3,
                    "theme": "Baroque Rome & Departure",
                    "aiInsight": "Toss a coin in the Trevi Fountain first thing — legend says it guarantees your return to Rome.",
                    "activities": [
                        {
                            "name": "Trevi Fountain & Spanish Steps",
                            "startTime": "08:00",
                            "endTime": "10:00",
                            "type": "Attraction",
                            "location": "Piazza di Trevi, Rome",
                            "notes": "Early morning visit avoids crowds. Enjoy a cornetto at a nearby bar.",
                            "costGbp": 0.00,
                            "confidenceScore": 0.93
                        },
                        {
                            "name": "Piazza Navona & Pantheon",
                            "startTime": "10:30",
                            "endTime": "13:00",
                            "type": "Attraction",
                            "location": "Piazza Navona, Rome",
                            "notes": "Pantheon entry now requires a timed ticket. Magnificent oculus on clear days.",
                            "costGbp": 5.00,
                            "confidenceScore": 0.95
                        },
                        {
                            "name": "Transfer to Fiumicino Airport",
                            "startTime": "15:00",
                            "endTime": "16:30",
                            "type": "Transfer",
                            "location": "Leonardo da Vinci International Airport",
                            "notes": "Allow 90 minutes for check-in and security. Leonardo Express train is quickest.",
                            "costGbp": 14.00,
                            "confidenceScore": 0.99
                        }
                    ]
                }
            ]
        }
        """;

    public Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
        => Task.FromResult(MockItineraryJson);
}

public sealed class MockBookingAutomationService : IBookingAutomationService
{
    public Task<BookingResult> BookAsync(Itinerary itinerary, CancellationToken cancellationToken = default)
        => Task.FromResult(new BookingResult
        {
            BookingReference = "TAI-MOCK-001",
            Status = BookingStatus.Confirmed,
            ConfirmationMessage = $"Mock booking confirmed for {itinerary.Destination}. Reference: TAI-MOCK-001",
            AutomationSteps = ["Mock: Flights reserved", "Mock: Hotels reserved", "Mock: Booking confirmed"]
        });

    public Task<BookingResult> CancelAsync(string bookingReference, CancellationToken cancellationToken = default)
        => Task.FromResult(new BookingResult
        {
            BookingReference = bookingReference,
            Status = BookingStatus.Confirmed,
            ConfirmationMessage = $"Mock cancellation processed for {bookingReference}.",
            AutomationSteps = ["Mock: Cancellation sent", "Mock: Refund initiated"]
        });
}
