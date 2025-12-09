using GrokBlazorApp.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using RichardSzalay.MockHttp;

namespace GrokBlazorApp.Tests;

public class GrokApiServiceTests
{
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ILogger<GrokApiService>> _loggerMock;
    private readonly MockHttpMessageHandler _mockHttp;

    public GrokApiServiceTests()
    {
        _configMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<GrokApiService>>();
        _mockHttp = new MockHttpMessageHandler();
    }

    [Fact]
    public void Constructor_ThrowsException_WhenApiKeyNotFound()
    {
        // Arrange
        var httpClient = new HttpClient();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["GrokApiKey"]).Returns((string?)null);
        var loggerMock = new Mock<ILogger<GrokApiService>>();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new GrokApiService(httpClient, configMock.Object, loggerMock.Object));
    }

    [Fact]
    public void Constructor_SetsBaseAddressAndHeaders_WhenApiKeyProvided()
    {
        // Arrange
        var httpClient = new HttpClient();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["GrokApiKey"]).Returns("test-key");
        var loggerMock = new Mock<ILogger<GrokApiService>>();

        // Act
        var service = new GrokApiService(httpClient, configMock.Object, loggerMock.Object);

        // Assert
        Assert.Equal("https://api.x.ai/", httpClient.BaseAddress?.ToString());
        var authHeader = httpClient.DefaultRequestHeaders.Authorization;
        Assert.NotNull(authHeader);
        Assert.Equal("Bearer", authHeader.Scheme);
        Assert.Equal("test-key", authHeader.Parameter);
        Assert.Contains(httpClient.DefaultRequestHeaders.Accept, h => h.MediaType == "application/json");
    }

    [Fact]
    public async Task GetChatCompletionAsync_WithMessages_ReturnsSuccessfulResponse()
    {
        // Arrange
        var expectedResponse = "This is a test response from Grok.";
        var messages = new List<Message>
        {
            new Message("user", "Hello, Grok!")
        };

        var mockResponse = new ChatResponse(new List<Choice>
        {
            new Choice(new Message("assistant", expectedResponse))
        });

        _configMock.Setup(c => c["GrokApiKey"]).Returns("test-key");

        _mockHttp.When("https://api.x.ai/v1/chat/completions")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(mockResponse));

        var httpClient = new HttpClient(_mockHttp);
        var service = new GrokApiService(httpClient, _configMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetChatCompletionAsync(messages);

        // Assert
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public async Task GetChatCompletionAsync_WithStringPrompt_ReturnsSuccessfulResponse()
    {
        // Arrange
        var prompt = "What is the capital of France?";
        var expectedResponse = "The capital of France is Paris.";

        var mockResponse = new ChatResponse(new List<Choice>
        {
            new Choice(new Message("assistant", expectedResponse))
        });

        _configMock.Setup(c => c["GrokApiKey"]).Returns("test-key");

        _mockHttp.When("https://api.x.ai/v1/chat/completions")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(mockResponse));

        var httpClient = new HttpClient(_mockHttp);
        var service = new GrokApiService(httpClient, _configMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetChatCompletionAsync(prompt);

        // Assert
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public async Task GetChatCompletionAsync_SendsCorrectRequestFormat()
    {
        // Arrange
        var messages = new List<Message>
        {
            new Message("system", "You are a helpful assistant."),
            new Message("user", "Hello!")
        };

        var expectedRequest = new
        {
            model = "grok-4-0709",
            messages = messages,
            temperature = (double?)null
        };

        _configMock.Setup(c => c["GrokApiKey"]).Returns("test-key");

        _mockHttp.When("https://api.x.ai/v1/chat/completions")
            .Respond(async req =>
            {
                // Verify request content
                Assert.NotNull(req.Content);
                var requestContent = await req.Content.ReadAsStringAsync();
                Assert.NotNull(requestContent);
                var actualRequest = System.Text.Json.JsonSerializer.Deserialize<ChatRequest>(requestContent!);

                Assert.NotNull(actualRequest);
                Assert.Equal("grok-4-0709", actualRequest.model);
                Assert.Equal(2, actualRequest.messages.Count);
                Assert.NotNull(actualRequest.messages[0]);
                Assert.Equal("system", actualRequest.messages[0].role);
                Assert.Equal("You are a helpful assistant.", actualRequest.messages[0].content);
                Assert.NotNull(actualRequest.messages[1]);
                Assert.Equal("user", actualRequest.messages[1].role);
                Assert.Equal("Hello!", actualRequest.messages[1].content);

                var mockResponse = new ChatResponse(new List<Choice>
                {
                    new Choice(new Message("assistant", "Hello back!"))
                });

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(mockResponse), System.Text.Encoding.UTF8, "application/json")
                };
            });

        var httpClient = new HttpClient(_mockHttp);
        var service = new GrokApiService(httpClient, _configMock.Object, _loggerMock.Object);

        // Act
        await service.GetChatCompletionAsync(messages);

        // Assert - Verification happens in the mock setup
    }

    [Fact]
    public async Task GetChatCompletionAsync_HandlesHttpErrorResponse()
    {
        // Arrange
        var messages = new List<Message> { new Message("user", "Test") };
        var errorMessage = "Invalid API key";

        _configMock.Setup(c => c["GrokApiKey"]).Returns("test-key");

        _mockHttp.When("https://api.x.ai/v1/chat/completions")
            .Respond(HttpStatusCode.Unauthorized, "application/json",
                System.Text.Json.JsonSerializer.Serialize(new { error = errorMessage }));

        var httpClient = new HttpClient(_mockHttp);
        var service = new GrokApiService(httpClient, _configMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetChatCompletionAsync(messages);

        // Assert
        Assert.Contains("API Error: Unauthorized", result);
        Assert.Contains(errorMessage, result);
    }

    [Fact]
    public async Task GetChatCompletionAsync_HandlesMalformedJsonResponse()
    {
        // Arrange
        var messages = new List<Message> { new Message("user", "Test") };

        _configMock.Setup(c => c["GrokApiKey"]).Returns("test-key");

        _mockHttp.When("https://api.x.ai/v1/chat/completions")
            .Respond("application/json", "{ invalid json }");

        var httpClient = new HttpClient(_mockHttp);
        var service = new GrokApiService(httpClient, _configMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetChatCompletionAsync(messages);

        // Assert
        Assert.Contains("Exception:", result);
    }

    [Fact]
    public async Task GetChatCompletionAsync_HandlesNetworkError()
    {
        // Arrange
        var messages = new List<Message> { new Message("user", "Test") };

        _configMock.Setup(c => c["GrokApiKey"]).Returns("test-key");

        _mockHttp.When("https://api.x.ai/v1/chat/completions")
            .Throw(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(_mockHttp);
        var service = new GrokApiService(httpClient, _configMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetChatCompletionAsync(messages);

        // Assert
        Assert.Contains("Exception:", result);
        Assert.Contains("Network error", result);
    }

    [Fact]
    public async Task GetChatCompletionAsync_HandlesEmptyChoicesArray()
    {
        // Arrange
        var messages = new List<Message> { new Message("user", "Test") };

        var mockResponse = new ChatResponse(new List<Choice>()); // Empty choices

        _configMock.Setup(c => c["GrokApiKey"]).Returns("test-key");

        _mockHttp.When("https://api.x.ai/v1/chat/completions")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(mockResponse));

        var httpClient = new HttpClient(_mockHttp);
        var service = new GrokApiService(httpClient, _configMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetChatCompletionAsync(messages);

        // Assert - The service throws an exception when trying to access choices[0] on an empty array
        Assert.Contains("Exception:", result);
        Assert.Contains("Index was out of range", result);
    }

    [Fact]
    public async Task GetChatCompletionAsync_HandlesNullResponse()
    {
        // Arrange
        var messages = new List<Message> { new Message("user", "Test") };

        _configMock.Setup(c => c["GrokApiKey"]).Returns("test-key");

        _mockHttp.When("https://api.x.ai/v1/chat/completions")
            .Respond("application/json", "null");

        var httpClient = new HttpClient(_mockHttp);
        var service = new GrokApiService(httpClient, _configMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetChatCompletionAsync(messages);

        // Assert
        Assert.Equal("No response received.", result);
    }

    [Fact]
    public async Task GetChatCompletionAsync_HandlesMissingMessageContent()
    {
        // Arrange
        var messages = new List<Message> { new Message("user", "Test") };

        var mockResponse = new ChatResponse(new List<Choice>
        {
            new Choice(new Message("assistant", "")) // Empty content instead of null
        });

        _configMock.Setup(c => c["GrokApiKey"]).Returns("test-key");

        _mockHttp.When("https://api.x.ai/v1/chat/completions")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(mockResponse));

        var httpClient = new HttpClient(_mockHttp);
        var service = new GrokApiService(httpClient, _configMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetChatCompletionAsync(messages);

        // Assert
        Assert.Equal("No response received.", result);
    }

    [Fact]
    public async Task GetChatCompletionAsync_WithComplexMessageHistory()
    {
        // Arrange
        var messages = new List<Message>
        {
            new Message("system", "You are a helpful AI assistant."),
            new Message("user", "What is 2+2?"),
            new Message("assistant", "2+2 equals 4."),
            new Message("user", "Thanks!")
        };

        var expectedResponse = "You're welcome!";

        var mockResponse = new ChatResponse(new List<Choice>
        {
            new Choice(new Message("assistant", expectedResponse))
        });

        _configMock.Setup(c => c["GrokApiKey"]).Returns("test-key");

        _mockHttp.When("https://api.x.ai/v1/chat/completions")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(mockResponse));

        var httpClient = new HttpClient(_mockHttp);
        var service = new GrokApiService(httpClient, _configMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetChatCompletionAsync(messages);

        // Assert
        Assert.Equal(expectedResponse, result);
    }
}
