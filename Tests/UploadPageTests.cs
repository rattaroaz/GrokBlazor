using Bunit;
using Bunit.TestDoubles;
using GrokBlazorApp.Components.Pages;
using GrokBlazorApp.Data;
using GrokBlazorApp.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace GrokBlazorApp.Tests;

public class UploadPageTests : BunitContext
{
    private readonly Mock<IDbContextFactory<ApplicationDbContext>> _mockDbFactory;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<AuthenticationStateProvider> _mockAuthProvider;

    public UploadPageTests()
    {
        _mockDbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);
        _mockAuthProvider = new Mock<AuthenticationStateProvider>();

        Services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(_mockDbFactory.Object);
        Services.AddSingleton<IServiceScopeFactory>(new TestServiceScopeFactory());
        Services.AddSingleton<NavigationManager>(new TestNavigationManager());
        Services.AddSingleton<ILogger<Upload>>(new TestLogger<Upload>());
        Services.AddSingleton<AuthenticationStateProvider>(_mockAuthProvider.Object);
        Services.AddSingleton(_mockUserManager.Object);
    }

    [Fact]
    public void UploadPage_RendersCorrectly_ForAuthorizedUser()
    {
        // Arrange
        SetupAuthenticatedUser();

        // Act
        var cut = Render<Upload>();

        // Assert
        Assert.Contains("Upload Personal Files", cut.Markup);
        Assert.Contains("Upload New File", cut.Markup);
        Assert.Contains("Your Files", cut.Markup);
    }

    [Fact]
    public void UploadPage_RendersCorrectly_ForUnauthorizedUser()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var cut = Render<Upload>();

        // Assert - Page should still render but may show different content
        Assert.Contains("Upload Personal Files", cut.Markup);
    }

    [Fact]
    public void UploadPage_DisplaysFileUploadForm()
    {
        // Arrange
        SetupAuthenticatedUser();

        // Act
        var cut = Render<Upload>();

        // Assert
        var fileInput = cut.Find("input[type='file']");
        Assert.NotNull(fileInput);

        var labelInput = cut.Find("input[placeholder]");
        Assert.Contains("Enter a label for the file", labelInput.GetAttribute("placeholder"));

        var submitButton = cut.Find("button[type='submit']");
        Assert.Equal("Upload", submitButton.TextContent.Trim());
    }

    private void SetupAuthenticatedUser()
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "test@example.com"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "test-user-id")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
        var user = new System.Security.Claims.ClaimsPrincipal(identity);

        _mockAuthProvider.Setup(ap => ap.GetAuthenticationStateAsync())
            .ReturnsAsync(new AuthenticationState(user));
    }

    private void SetupUnauthenticatedUser()
    {
        var identity = new System.Security.Claims.ClaimsIdentity();
        var user = new System.Security.Claims.ClaimsPrincipal(identity);

        _mockAuthProvider.Setup(ap => ap.GetAuthenticationStateAsync())
            .ReturnsAsync(new AuthenticationState(user));
    }
}

// Test helper classes
public class TestServiceScopeFactory : IServiceScopeFactory
{
    public IServiceScope CreateScope() => new TestServiceScope();
}

public class TestServiceScope : IServiceScope
{
    public IServiceProvider ServiceProvider => new TestServiceProvider();
    public void Dispose() { }
}

public class TestServiceProvider : IServiceProvider
{
    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(UserManager<ApplicationUser>))
        {
            return new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(),
                null!, null!, null!, null!, null!, null!, null!, null!).Object;
        }
        return null;
    }
}

public class TestNavigationManager : NavigationManager
{
    public TestNavigationManager()
    {
        Initialize("http://localhost/", "http://localhost/");
    }
}

public class TestLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // No-op for testing
    }
}

public class MockBrowserFile : IBrowserFile
{
    public MockBrowserFile(string name, string contentType, string content)
    {
        Name = name;
        ContentType = contentType;
        Size = content.Length;
        _content = content;
    }

    private readonly string _content;

    public string Name { get; }
    public DateTimeOffset LastModified => DateTimeOffset.Now;
    public long Size { get; }
    public string ContentType { get; }
    public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
    {
        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_content));
    }
}
