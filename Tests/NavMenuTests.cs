using Bunit;
using GrokBlazorApp.Components.Layout;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GrokBlazorApp.Tests;

public class NavMenuTests : BunitContext
{
    public NavMenuTests()
    {
        // Add required authorization services
        Services.AddLogging();
        Services.AddOptions();
        Services.AddAuthorizationCore();
        Services.AddSingleton<IAuthorizationService, DefaultAuthorizationService>();
        Services.AddSingleton<IAuthorizationPolicyProvider, DefaultAuthorizationPolicyProvider>();
        Services.AddSingleton<IAuthorizationHandlerProvider, DefaultAuthorizationHandlerProvider>();
        Services.AddSingleton<AuthenticationStateProvider, FakeAuthenticationStateProvider>();
    }

    [Fact]
    public void NavMenu_RendersCorrectly()
    {
        // Act
        var cut = Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<NavMenu>()
        );

        // Assert
        Assert.Contains("GrokBlazorApp", cut.Markup);
        Assert.Contains("Home", cut.Markup);
    }

    [Fact]
    public void NavMenu_ContainsHomeLink()
    {
        // Act
        var cut = Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<NavMenu>()
        );

        // Assert
        var homeLinks = cut.FindAll("a[href='']");
        Assert.NotEmpty(homeLinks);
    }
}

// Fake AuthenticationStateProvider for testing
public class FakeAuthenticationStateProvider : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var anonymous = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());
        return Task.FromResult(new AuthenticationState(anonymous));
    }
}
