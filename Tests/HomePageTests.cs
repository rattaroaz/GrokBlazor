using Bunit;
using GrokBlazorApp.Components.Pages;
using Xunit;

namespace GrokBlazorApp.Tests;

public class HomePageTests : BunitContext
{
    [Fact]
    public void HomePageRendersCorrectly()
    {
        // Render the Home component
        var cut = Render<Home>();

        // Verify that the heading is rendered
        Assert.Contains("Grok API Chat", cut.Markup);
    }
}
