using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace TravelAI.Core.Providers;

public sealed class OpenAIProvider : ILlmProvider
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<OpenAIProvider> _logger;

    public OpenAIProvider(string apiKey, string model, ILogger<OpenAIProvider> logger)
    {
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("OpenAI: calling model {Model}", _model);
        var client = new OpenAIClient(_apiKey);
        var chatClient = client.GetChatClient(_model);
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
