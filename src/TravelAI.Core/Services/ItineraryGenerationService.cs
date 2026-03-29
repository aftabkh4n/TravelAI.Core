using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using TravelAI.Core.Interfaces;
using TravelAI.Core.Models;

namespace TravelAI.Core.Services;

public sealed class ItineraryGenerationService : IItineraryGenerationService
{
    private readonly AzureOpenAIClient _client;
    private readonly ItineraryGenerationOptions _options;
    private readonly ILogger<ItineraryGenerationService> _logger;

    public ItineraryGenerationService(
        AzureOpenAIClient client,
        IOptions<ItineraryGenerationOptions> options,
        ILogger<ItineraryGenerationService> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Itinerary> GenerateAsync(
        TravellerProfile traveller,
        string destination,
        DateOnly departure,
        DateOnly returnDate,
        string? additionalInstructions = null,
        CancellationToken cancellationToken = default)
    {
        var duration = returnDate.DayNumber - departure.DayNumber;
        _logger.LogInformation("Generating {Duration}-day itinerary for {Traveller} to {Destination}", duration, traveller.Name, destination);

        var chatClient = _client.GetChatClient(_options.DeploymentName);
        var response = await chatClient.CompleteChatAsync(
            [
                new SystemChatMessage(BuildSystemPrompt(traveller)),
                new UserChatMessage(BuildUserPrompt(traveller, destination, departure, returnDate, duration, additionalInstructions))
            ],
            new ChatCompletionOptions
            {
                Temperature = 0.7f,
                MaxOutputTokenCount = 4096,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            },
            cancellationToken);

        return ParseItineraryResponse(response.Value.Content[0].Text, traveller, destination, departure, returnDate);
    }

    public async Task<Itinerary> RefineAsync(Itinerary existing, string feedback, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refining itinerary {Id}", existing.Id);
        var chatClient = _client.GetChatClient(_options.DeploymentName);
        var response = await chatClient.CompleteChatAsync(
            [
                new SystemChatMessage("You are a travel expert. Refine the itinerary based on user feedback. Respond with a complete updated itinerary in JSON format."),
                new UserChatMessage($"Current itinerary:\n{JsonSerializer.Serialize(existing)}\n\nFeedback: {feedback}")
            ],
            new ChatCompletionOptions { Temperature = 0.5f, MaxOutputTokenCount = 4096, ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() },
            cancellationToken);

        return ParseItineraryResponse(response.Value.Content[0].Text, existing.Traveller, existing.Destination, existing.DepartureDate, existing.ReturnDate);
    }

    private static string BuildSystemPrompt(TravellerProfile traveller) => $"""
        You are an expert AI travel planner for UK-based travellers.
        Traveller tier: {traveller.Tier}
        Dietary requirements: {string.Join(", ", traveller.DietaryRequirements.DefaultIfEmpty("None"))}
        Always respond with valid JSON. Include realistic GBP cost estimates.
        """;

    private static string BuildUserPrompt(TravellerProfile traveller, string destination, DateOnly departure, DateOnly returnDate, int duration, string? extra) =>
        new StringBuilder()
            .AppendLine($"Create a {duration}-day itinerary to {destination}")
            .AppendLine($"Departure: {departure:d MMMM yyyy}, Return: {returnDate:d MMMM yyyy}")
            .AppendLine($"Traveller: {traveller.Name}")
            .AppendLine($"Preferences: {string.Join(", ", traveller.Preferences.DefaultIfEmpty("None"))}")
            .AppendLine(extra ?? string.Empty)
            .AppendLine("Return JSON with: title, aiSummary, estimatedCostGbp, days[].")
            .AppendLine("Each day: dayNumber, date, theme, aiInsight, activities[].")
            .AppendLine("Each activity: name, startTime, endTime, type, location, notes, costGbp, confidenceScore.")
            .ToString();

    private static Itinerary ParseItineraryResponse(string json, TravellerProfile traveller, string destination, DateOnly departure, DateOnly returnDate)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var days = new List<ItineraryDay>();

        if (root.TryGetProperty("days", out var daysEl))
        {
            foreach (var day in daysEl.EnumerateArray())
            {
                var activities = new List<Activity>();
                if (day.TryGetProperty("activities", out var actsEl))
                {
                    foreach (var act in actsEl.EnumerateArray())
                    {
                        activities.Add(new Activity
                        {
                            Name = act.TryGetString("name") ?? "Activity",
                            StartTime = TimeOnly.TryParse(act.TryGetString("startTime"), out var st) ? st : new TimeOnly(9, 0),
                            EndTime = TimeOnly.TryParse(act.TryGetString("endTime"), out var et) ? et : new TimeOnly(17, 0),
                            Type = Enum.TryParse<ActivityType>(act.TryGetString("type"), true, out var at) ? at : ActivityType.Leisure,
                            Location = act.TryGetString("location"),
                            Notes = act.TryGetString("notes"),
                            CostGbp = act.TryGetDecimal("costGbp"),
                            ConfidenceScore = act.TryGetDouble("confidenceScore")
                        });
                    }
                }
                var dayNum = day.TryGetProperty("dayNumber", out var dn) && dn.TryGetInt32(out var d) ? d : 1;
                days.Add(new ItineraryDay { DayNumber = dayNum, Date = departure.AddDays(dayNum - 1), Activities = activities, Theme = day.TryGetString("theme"), AiInsight = day.TryGetString("aiInsight") });
            }
        }

        return new Itinerary
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = root.TryGetString("title") ?? $"{destination} Adventure",
            Destination = destination,
            DepartureDate = departure,
            ReturnDate = returnDate,
            Traveller = traveller,
            Days = days,
            EstimatedCostGbp = root.TryGetDecimal("estimatedCostGbp") ?? 0m,
            AiSummary = root.TryGetString("aiSummary"),
            Status = ItineraryStatus.Draft
        };
    }
}

public sealed class ItineraryGenerationOptions
{
    public const string SectionName = "TravelAI:ItineraryGeneration";
    public required string DeploymentName { get; set; }
}

internal static class JsonElementExtensions
{
    public static string? TryGetString(this JsonElement el, string property) =>
        el.TryGetProperty(property, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    public static decimal? TryGetDecimal(this JsonElement el, string property) =>
        el.TryGetProperty(property, out var p) && p.TryGetDecimal(out var v) ? v : null;
    public static double? TryGetDouble(this JsonElement el, string property) =>
        el.TryGetProperty(property, out var p) && p.TryGetDouble(out var v) ? v : null;
}
