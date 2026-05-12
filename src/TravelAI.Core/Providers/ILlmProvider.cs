namespace TravelAI.Core.Providers;

public interface ILlmProvider
{
    Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}

public enum ProviderType { AzureOpenAI, OpenAI, Anthropic, Ollama, Mock }
