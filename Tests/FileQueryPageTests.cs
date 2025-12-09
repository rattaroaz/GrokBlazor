using Bunit;
using Bunit.TestDoubles;
using GrokBlazorApp.Components.Pages;
using GrokBlazorApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.IO;
using System.Text;

namespace GrokBlazorApp.Tests;

public class FileQueryPageTests : BunitContext
{
    private readonly Mock<IGrokApiService> _mockGrokService;

    public FileQueryPageTests()
    {
        _mockGrokService = new Mock<IGrokApiService>();
        Services.AddSingleton<IGrokApiService>(_mockGrokService.Object);
    }

    [Fact]
    public void FileQueryPage_RendersCorrectly_Initially()
    {
        // Act
        var cut = Render<FileQuery>();

        // Assert
        Assert.Contains("Upload files:", cut.Markup);
        Assert.Contains("Enter your question:", cut.Markup);
        Assert.Contains("Send", cut.Markup);
        Assert.DoesNotContain("Selected Files:", cut.Markup);
    }

    [Fact]
    public void FileQueryPage_DisplaysInputFields()
    {
        // Act
        var cut = Render<FileQuery>();

        // Assert
        var fileInput = cut.Find("input[id='files']");
        Assert.NotNull(fileInput);
        Assert.True(fileInput.HasAttribute("multiple"));

        var questionInput = cut.Find("input[id='question']");
        Assert.NotNull(questionInput);

        var sendButton = cut.Find("button");
        Assert.Equal("Send", sendButton.TextContent);
    }

    [Fact]
    public void FileQueryPage_ShowsLoadingState_InitiallyHidden()
    {
        // Act
        var cut = Render<FileQuery>();

        // Assert - Loading should not be visible initially
        Assert.DoesNotContain("Loading...", cut.Markup);
    }

    [Fact]
    public void FileQueryPage_ShowsChatHistory_InitiallyEmpty()
    {
        // Act
        var cut = Render<FileQuery>();

        // Assert - No chat history initially
        Assert.DoesNotContain("You:", cut.Markup);
        Assert.DoesNotContain("Grok:", cut.Markup);
    }

    [Fact]
    public void FileQueryPage_HandlesEmptyQuestionInput()
    {
        // Act
        var cut = Render<FileQuery>();

        // Assert
        var questionInput = cut.Find("input[id='question']");
        Assert.Equal("", questionInput.GetAttribute("value"));
    }
}
