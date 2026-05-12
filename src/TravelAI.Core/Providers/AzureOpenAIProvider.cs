using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace TravelAI.Core.Providers;

public sealed class AzureOpenAIProvider : ILlmProvider
{
    private readonly AzureOpenAIClient _client;
    private readonly string _deployment;
    private readonly ILogger<AzureOpenAIProvider> _logger;

    public AzureOpenAIProvider(AzureOpenAIClient client, string deployment, ILogger<AzureOpenAIProvider> logger)
    {
        _client = client;
        _deployment = deployment;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("AzureOpenAI: calling deployment {Deployment}", _deployment);
        var chatClient = _client.GetChatClient(_deployment);
        var response = await chatClient.CompleteChatAsync(
            [new SystemChatMessage(systemPrompt), new UserChatMessage(userPrompt)],
            new ChatCompletionOptions
            {
                Temperature = 0.7f,
                MaxOutputTokenCount = 4096,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            },
            cancellationToken);
        return response.Value.Content[0].Text;
    }
}
