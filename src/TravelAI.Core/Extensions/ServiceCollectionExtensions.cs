using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TravelAI.Core.HealthChecks;
using TravelAI.Core.Interfaces;
using TravelAI.Core.Middleware;
using TravelAI.Core.Providers;
using TravelAI.Core.Services;

namespace TravelAI.Core.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all TravelAI.Core services using IConfiguration (Azure OpenAI + Azure AI Search).
    /// Call in Program.cs: builder.Services.AddTravelAI(builder.Configuration);
    /// </summary>
    public static IServiceCollection AddTravelAI(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ItineraryGenerationOptions>(o => configuration.GetSection(ItineraryGenerationOptions.SectionName).Bind(o));
        services.Configure<PriceAnomalyOptions>(o => configuration.GetSection(PriceAnomalyOptions.SectionName).Bind(o));
        services.Configure<DestinationSearchOptions>(o => configuration.GetSection(DestinationSearchOptions.SectionName).Bind(o));
        services.Configure<BookingAutomationOptions>(o => configuration.GetSection(BookingAutomationOptions.SectionName).Bind(o));

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
            var opts = sp.GetRequiredService<IOptions<DestinationSearchOptions>>();
            return new SearchClient(new Uri(endpoint), opts.Value.IndexName, new AzureKeyCredential(key));
        });

        services.AddScoped<ILlmProvider>(sp =>
        {
            var azureClient = sp.GetRequiredService<AzureOpenAIClient>();
            var deploymentName = sp.GetRequiredService<IOptions<ItineraryGenerationOptions>>().Value.DeploymentName;
            var logger = sp.GetRequiredService<ILogger<AzureOpenAIProvider>>();
            return new AzureOpenAIProvider(azureClient, deploymentName, logger);
        });

        services.AddScoped<IItineraryGenerationService, ItineraryGenerationService>();
        services.AddScoped<IPriceAnomalyDetector, PriceAnomalyDetector>();
        services.AddScoped<IDestinationSearchService, DestinationSearchService>();
        services.AddScoped<IBookingAutomationService, BookingAutomationService>();

        return services;
    }

    /// <summary>
    /// Registers TravelAI.Core services using fluent provider configuration.
    /// Example: builder.Services.AddTravelAI(o => o.UseMock());
    /// </summary>
    public static IServiceCollection AddTravelAI(this IServiceCollection services, Action<TravelAIOptions> configure)
    {
        var travelOptions = new TravelAIOptions();
        configure(travelOptions);

        services.Configure<PriceAnomalyOptions>(_ => { });
        services.Configure<BookingAutomationOptions>(_ => { });

        switch (travelOptions.ProviderType)
        {
            case ProviderType.AzureOpenAI:
                services.AddSingleton(new AzureOpenAIClient(
                    new Uri(travelOptions.AzureEndpoint!),
                    new AzureKeyCredential(travelOptions.AzureKey!)));
                services.AddScoped<ILlmProvider>(sp => new AzureOpenAIProvider(
                    sp.GetRequiredService<AzureOpenAIClient>(),
                    travelOptions.AzureDeployment!,
                    sp.GetRequiredService<ILogger<AzureOpenAIProvider>>()));
                break;

            case ProviderType.OpenAI:
                services.AddScoped<ILlmProvider>(sp => new OpenAIProvider(
                    travelOptions.OpenAIApiKey!,
                    travelOptions.OpenAIModel!,
                    sp.GetRequiredService<ILogger<OpenAIProvider>>()));
                break;

            case ProviderType.Anthropic:
                services.AddScoped<ILlmProvider>(sp => new AnthropicProvider(
                    travelOptions.AnthropicApiKey!,
                    travelOptions.AnthropicModel!,
                    sp.GetRequiredService<ILogger<AnthropicProvider>>()));
                break;

            case ProviderType.Ollama:
                services.AddHttpClient();
                services.AddScoped<ILlmProvider>(sp => new OllamaProvider(
                    sp.GetRequiredService<IHttpClientFactory>(),
                    travelOptions.OllamaBaseUrl!,
                    travelOptions.OllamaModel!,
                    sp.GetRequiredService<ILogger<OllamaProvider>>()));
                break;

            case ProviderType.Mock:
                services.AddScoped<ILlmProvider, MockProvider>();
                services.AddScoped<IDestinationSearchService, MockDestinationSearchService>();
                services.AddScoped<IBookingAutomationService, MockBookingAutomationService>();
                break;
        }

        services.AddScoped<IItineraryGenerationService, ItineraryGenerationService>();
        services.AddScoped<IPriceAnomalyDetector, PriceAnomalyDetector>();

        if (travelOptions.ProviderType != ProviderType.Mock)
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
