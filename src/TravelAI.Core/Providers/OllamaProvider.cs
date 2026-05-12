using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TravelAI.Core.Providers;

public sealed class OllamaProvider : ILlmProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly ILogger<OllamaProvider> _logger;

    public OllamaProvider(IHttpClientFactory httpClientFactory, string baseUrl, string model, ILogger<OllamaProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Ollama: calling {Model} at {Url}", _model, _baseUrl);
        var client = _httpClientFactory.CreateClient();
        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = false,
            format = "json"
        };
        var httpResponse = await client.PostAsJsonAsync($"{_baseUrl}/api/chat", requestBody, cancellationToken);
        httpResponse.EnsureSuccessStatusCode();
        var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("message").GetProperty("content").GetString()
            ?? throw new InvalidOperationException("Ollama returned empty content");
    }
}
