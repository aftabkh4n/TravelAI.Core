using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace TravelAI.Core.HealthChecks;

public sealed class AzureOpenAIHealthCheck(AzureOpenAIClient client, ILogger<AzureOpenAIHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var chatClient = client.GetChatClient("gpt-4o");
            var response = await chatClient.CompleteChatAsync(
                [new UserChatMessage("ping")],
                new ChatCompletionOptions { MaxOutputTokenCount = 1 },
                cancellationToken);
            return response.Value is not null
                ? HealthCheckResult.Healthy("Azure OpenAI is reachable")
                : HealthCheckResult.Degraded("Azure OpenAI returned null");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Azure OpenAI health check failed");
            return HealthCheckResult.Unhealthy("Azure OpenAI unreachable", ex);
        }
    }
}

public sealed class AzureSearchHealthCheck(SearchClient searchClient, ILogger<AzureSearchHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await searchClient.SearchAsync<Azure.Search.Documents.Models.SearchDocument>(
                "*", new Azure.Search.Documents.SearchOptions { Size = 1 }, cancellationToken);
            return HealthCheckResult.Healthy($"Azure AI Search '{searchClient.IndexName}' is reachable");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Azure AI Search health check failed");
            return HealthCheckResult.Unhealthy($"Azure AI Search '{searchClient.IndexName}' unreachable", ex);
        }
    }
}
