using MassTransit;
using TravelAI.Core.Interfaces;
using TravelAI.Core.Messages;
using TravelAI.Core.Models;

namespace TravelAI.AiWorker;

public class ItineraryRequestedConsumer(
    IItineraryGenerationService aiService,
    ILogger<ItineraryRequestedConsumer> logger) : IConsumer<ItineraryRequested>
{
    public async Task Consume(ConsumeContext<ItineraryRequested> context)
    {
        var msg = context.Message;

        logger.LogInformation(
            "Generating itinerary {CorrelationId} for {Traveller} to {Destination}",
            msg.CorrelationId, msg.TravellerName, msg.Destination);

        try
        {
            var traveller = new TravellerProfile
            {
                Name        = msg.TravellerName,
                Email       = msg.TravellerEmail,
                Preferences = [],
                DietaryRequirements = [],
                Tier        = TravelTier.Standard
            };

            var itinerary = await aiService.GenerateAsync(
                traveller,
                msg.Destination,
                msg.Departure,
                msg.ReturnDate,
                msg.AdditionalInstructions,
                context.CancellationToken);

            await context.Publish(new ItineraryGenerated(
                msg.CorrelationId,
                itinerary,
                Success: true,
                ErrorMessage: null));

            logger.LogInformation(
                "Itinerary {CorrelationId} generated successfully",
                msg.CorrelationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Itinerary generation {CorrelationId} failed",
                msg.CorrelationId);

            await context.Publish(new ItineraryGenerated(
                msg.CorrelationId,
                new object(),
                Success: false,
                ErrorMessage: ex.Message));

            throw;
        }
    }
}