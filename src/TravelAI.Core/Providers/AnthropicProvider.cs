using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;

namespace TravelAI.Core.Providers;

public sealed class AnthropicProvider : ILlmProvider
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<AnthropicProvider> _logger;

    public AnthropicProvider(string apiKey, string model, ILogger<AnthropicProvider> logger)
    {
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Anthropic: calling model {Model}", _model);
        var client = new AnthropicClient(_apiKey);
        var parameters = new MessageParameters
        {
            Model = _model,
            MaxTokens = 4096,
            System = [new SystemMessage(systemPrompt)],
            Messages =
            [
                new Message
                {
                    Role = RoleType.User,
                    Content = [new TextContent { Text = userPrompt }]
                }
            ]
        };
        var response = await client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);
        return response.Content.OfType<TextContent>().First().Text;
    }
}
