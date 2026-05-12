using TravelAI.Core.Providers;

namespace TravelAI.Core;

public sealed class TravelAIOptions
{
    internal ProviderType ProviderType { get; private set; } = ProviderType.AzureOpenAI;
    internal string? AzureEndpoint { get; private set; }
    internal string? AzureKey { get; private set; }
    internal string? AzureDeployment { get; private set; }
    internal string? OpenAIApiKey { get; private set; }
    internal string? OpenAIModel { get; private set; }
    internal string? AnthropicApiKey { get; private set; }
    internal string? AnthropicModel { get; private set; }
    internal string? OllamaBaseUrl { get; private set; }
    internal string? OllamaModel { get; private set; }

    public TravelAIOptions UseAzureOpenAI(string endpoint, string key, string deployment)
    {
        ProviderType = ProviderType.AzureOpenAI;
        AzureEndpoint = endpoint;
        AzureKey = key;
        AzureDeployment = deployment;
        return this;
    }

    public TravelAIOptions UseOpenAI(string apiKey, string model = "gpt-4o")
    {
        ProviderType = ProviderType.OpenAI;
        OpenAIApiKey = apiKey;
        OpenAIModel = model;
        return this;
    }

    public TravelAIOptions UseAnthropic(string apiKey, string model = "claude-3-5-sonnet-20241022")
    {
        ProviderType = ProviderType.Anthropic;
        AnthropicApiKey = apiKey;
        AnthropicModel = model;
        return this;
    }

    public TravelAIOptions UseOllama(string baseUrl, string model = "llama3.2")
    {
        ProviderType = ProviderType.Ollama;
        OllamaBaseUrl = baseUrl;
        OllamaModel = model;
        return this;
    }

    public TravelAIOptions UseMock()
    {
        ProviderType = ProviderType.Mock;
        return this;
    }
}
