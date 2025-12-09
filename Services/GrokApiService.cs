using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GrokBlazorApp.Services;

public record Message(string role, string content);
public record ChatRequest(string model, List<Message> messages, double? temperature = null);
public record Choice(Message message);
public record ChatResponse(List<Choice> choices);

public class GrokApiService : IGrokApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GrokApiService> _logger;

    public GrokApiService(HttpClient httpClient, IConfiguration configuration, ILogger<GrokApiService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GrokApiKey"] ?? throw new InvalidOperationException("Grok API key not found in configuration.");
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://api.x.ai/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<string> GetChatCompletionAsync(List<Message> messages)
    {
        try
        {
            var request = new ChatRequest("grok-4-0709", messages);
            _logger.LogInformation("Sending request to Grok API with {MessageCount} messages", messages.Count);
            var httpResponse = await _httpClient.PostAsJsonAsync("v1/chat/completions", request);
            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Grok API returned {StatusCode}: {Error}", httpResponse.StatusCode, errorContent);
                return $"API Error: {httpResponse.StatusCode} - {errorContent}";
            }
            var chatResponse = await httpResponse.Content.ReadFromJsonAsync<ChatResponse>();
            _logger.LogInformation("Received response from Grok API");
            return string.IsNullOrEmpty(chatResponse?.choices?[0]?.message?.content) ? "No response received." : chatResponse.choices[0].message.content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Grok API");
            return $"Exception: {ex.Message}";
        }
    }

    public async Task<string> GetChatCompletionAsync(string userPrompt)
    {
        return await GetChatCompletionAsync(new List<Message> { new Message("user", userPrompt) });
    }
}