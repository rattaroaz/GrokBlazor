using Bunit;
using Bunit.TestDoubles;
using GrokBlazorApp.Components.Pages;
using GrokBlazorApp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace GrokBlazorApp.Tests;

public class QueryPageTests : BunitContext
{
    private readonly Mock<IGrokApiService> _mockService;

    public QueryPageTests()
    {
        _mockService = new Mock<IGrokApiService>();
        Services.AddSingleton<IGrokApiService>(_mockService.Object);
    }

    [Fact]
    public void QueryPage_RendersCorrectly_Initially()
    {
        // Act
        var cut = Render<Query>();

        // Assert
        cut.MarkupMatches(@"
<div>
    <label for=""prompt"">Enter your prompt:</label>
    <input id=""prompt"" value="""" >
    <button >Send</button>
</div>
");
    }

    [Fact]
    public void QueryPage_SendButton_TriggersApiCall_AndUpdatesHistory()
    {
        // Arrange
        var cut = Render<Query>();
        const string testPrompt = "Test prompt";
        const string testResponse = "Test response";

        _mockService.Setup(s => s.GetChatCompletionAsync(It.IsAny<List<Message>>()))
            .ReturnsAsync(testResponse);

        // Act: Set input value and click send
        var input = cut.Find("input");
        input.Change(testPrompt);

        var button = cut.Find("button");
        button.Click();

        // Assert: Check final chat history
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("You:", cut.Markup);
            Assert.Contains(testPrompt, cut.Markup);
            Assert.Contains("Grok:", cut.Markup);
            Assert.Contains(testResponse, cut.Markup);
        });

        _mockService.Verify(s => s.GetChatCompletionAsync(It.IsAny<List<Message>>()), Times.Once);
    }

    [Fact]
    public void QueryPage_SendButton_EmptyPrompt_DoesNothing()
    {
        // Arrange
        var cut = Render<Query>();

        // Act: Click send without entering prompt
        var button = cut.Find("button");
        button.Click();

        // Assert: No API call made
        _mockService.Verify(s => s.GetChatCompletionAsync(It.IsAny<List<Message>>()), Times.Never);
    }
}
