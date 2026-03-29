using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TravelAI.Core.HealthChecks;
using TravelAI.Core.Interfaces;
using TravelAI.Core.Middleware;
using TravelAI.Core.Services;

namespace TravelAI.Core.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all TravelAI.Core services. Call in Program.cs: builder.Services.AddTravelAI(builder.Configuration);
    /// </summary>
    public static IServiceCollection AddTravelAI(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ItineraryGenerationOptions>(configuration.GetSection(ItineraryGenerationOptions.SectionName));
        services.Configure<PriceAnomalyOptions>(configuration.GetSection(PriceAnomalyOptions.SectionName));
        services.Configure<DestinationSearchOptions>(configuration.GetSection(DestinationSearchOptions.SectionName));
        services.Configure<BookingAutomationOptions>(configuration.GetSection(BookingAutomationOptions.SectionName));

        services.AddSingleton<AzureOpenAIClient>(sp =>
        {
            var endpoint = configuration["TravelAI:AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("TravelAI:AzureOpenAI:Endpoint is required");
            var key = configuration["TravelAI:AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("TravelAI:AzureOpenAI:ApiKey is required");
            return new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));
        });

        services.AddSingleton<SearchClient>(sp =>
        {
            var endpoint = configuration["TravelAI:AzureSearch:Endpoint"] ?? throw new InvalidOperationException("TravelAI:AzureSearch:Endpoint is required");
            var key = configuration["TravelAI:AzureSearch:ApiKey"] ?? throw new InvalidOperationException("TravelAI:AzureSearch:ApiKey is required");
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DestinationSearchOptions>>();
            return new SearchClient(new Uri(endpoint), opts.Value.IndexName, new AzureKeyCredential(key));
        });

        services.AddScoped<IItineraryGenerationService, ItineraryGenerationService>();
        services.AddScoped<IPriceAnomalyDetector, PriceAnomalyDetector>();
        services.AddScoped<IDestinationSearchService, DestinationSearchService>();
        services.AddScoped<IBookingAutomationService, BookingAutomationService>();

        return services;
    }

    /// <summary>
    /// Registers health checks for Azure OpenAI and Azure AI Search.
    /// Exposes /health/live (liveness) and /health/ready (readiness).
    /// </summary>
    public static IHealthChecksBuilder AddTravelAIHealthChecks(this IServiceCollection services) =>
        services.AddHealthChecks()
            .AddCheck<AzureOpenAIHealthCheck>("azure-openai", tags: ["ready", "ai"])
            .AddCheck<AzureSearchHealthCheck>("azure-search", tags: ["ready", "ai"]);

    /// <summary>
    /// Adds observability middleware and AI rate limiting. Call after UseRouting() in Program.cs.
    /// </summary>
    public static IApplicationBuilder UseTravelAIObservability(this IApplicationBuilder app)
    {
        app.UseMiddleware<TravelAIObservabilityMiddleware>();
        app.UseMiddleware<AiRateLimitMiddleware>();
        return app;
    }
}
