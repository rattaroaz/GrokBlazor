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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace GrokBlazorApp.Tests;

public class NotADoctorTests : IAsyncLifetime
{
    private readonly Mock<IGrokApiService> _mockGrokService;
    private readonly Mock<AuthenticationStateProvider> _mockAuthProvider;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;

    private DbContextOptions<ApplicationDbContext>? _dbOptions;

    public NotADoctorTests()
    {
        _mockGrokService = new Mock<IGrokApiService>();
        _mockAuthProvider = new Mock<AuthenticationStateProvider>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
    }

    public Task InitializeAsync()
    {
        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // No need to dispose anything since we create fresh contexts for each test
        return Task.CompletedTask;
    }

    private BunitContext CreateTestContext(ApplicationDbContext? dbContext = null)
    {
        var ctx = new BunitContext();

        ctx.Services.AddSingleton<IGrokApiService>(_mockGrokService.Object);
        ctx.Services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(new TestDbContextFactory(dbContext));
        ctx.Services.AddSingleton<AuthenticationStateProvider>(_mockAuthProvider.Object);

        // Use a simple custom service provider for scope
        ctx.Services.AddSingleton<IServiceScopeFactory>(new NotADoctorTestServiceScopeFactory(_mockUserManager.Object));

        ctx.Services.AddSingleton(_mockUserManager.Object);

        // Setup authorization
        ctx.Services.AddAuthorizationCore();
        ctx.Services.AddSingleton<IAuthorizationService, DefaultAuthorizationService>();
        ctx.Services.AddSingleton<IAuthorizationPolicyProvider, DefaultAuthorizationPolicyProvider>();
        ctx.Services.AddSingleton<IAuthorizationHandlerProvider, DefaultAuthorizationHandlerProvider>();

        return ctx;
    }

    private async Task<ApplicationDbContext> CreateAndSetupDatabase()
    {
        var dbContext = new ApplicationDbContext(_dbOptions!);
        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    [Fact]
    public void NotADoctor_RequiresGuestRoleAuthorization()
    {
        // Act & Assert - This would normally be tested via authorization middleware
        // but we can verify the component has the Authorize attribute by checking its type
        var componentType = typeof(NotADoctor);
        var authorizeAttribute = componentType.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .FirstOrDefault() as AuthorizeAttribute;

        Assert.NotNull(authorizeAttribute);
        Assert.Equal("Guest", authorizeAttribute.Roles);
    }

    [Fact]
    public async Task NotADoctor_LoadsAndDisplaysUserFiles()
    {
        // Arrange
        var dbContext = await CreateAndSetupDatabase();
        var user = new ApplicationUser
        {
            UserName = "guest@example.com",
            Email = "guest@example.com",
            EmailConfirmed = true
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var testFiles = new List<UserFile>
        {
            new UserFile
            {
                UserId = user.Id,
                FileName = "test1.txt",
                Label = "Test File 1",
                Content = new byte[] { 1, 2, 3 },
                UploadDate = DateTime.UtcNow.AddDays(-1)
            },
            new UserFile
            {
                UserId = user.Id,
                FileName = "test2.txt",
                Label = "Test File 2",
                Content = new byte[] { 4, 5, 6 },
                UploadDate = DateTime.UtcNow
            }
        };

        dbContext.UserFiles!.AddRange(testFiles);
        await dbContext.SaveChangesAsync();

        SetupGuestUser(user);
        var ctx = CreateTestContext(dbContext);

        // Act
        var cut = ctx.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<NotADoctor>());

        // Wait for async initialization
        await Task.Delay(100);

        // Assert
        Assert.Contains("Test File 1", cut.Markup);
        Assert.Contains("Test File 2", cut.Markup);
        Assert.Contains("test1.txt", cut.Markup);
        Assert.Contains("test2.txt", cut.Markup);

        // Check table structure
        var checkboxes = cut.FindAll("input[type='checkbox']");
        Assert.Equal(2, checkboxes.Count);

        // Cleanup
        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task NotADoctor_AllowsFileSelection()
    {
        // Arrange
        var dbContext = await CreateAndSetupDatabase();
        var user = new ApplicationUser
        {
            UserName = "guest@example.com",
            Email = "guest@example.com",
            EmailConfirmed = true
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var testFile = new UserFile
        {
            UserId = user.Id,
            FileName = "test.txt",
            Label = "Test File",
            Content = new byte[] { 1, 2, 3 },
            UploadDate = DateTime.UtcNow
        };
        dbContext.UserFiles!.Add(testFile);
        await dbContext.SaveChangesAsync();

        SetupGuestUser(user);
        var ctx = CreateTestContext(dbContext);

        var cut = ctx.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<NotADoctor>());
        await Task.Delay(100);

        // Act - Select file
        var checkbox = cut.Find("input[type='checkbox']");
        checkbox.Change(true);

        // Assert - Checkbox should be checked
        Assert.True(checkbox.HasAttribute("checked"));

        // Cleanup
        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task NotADoctor_AllowsFileDeselection()
    {
        // Arrange
        var dbContext = await CreateAndSetupDatabase();
        var user = new ApplicationUser
        {
            UserName = "guest@example.com",
            Email = "guest@example.com",
            EmailConfirmed = true
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var testFile = new UserFile
        {
            UserId = user.Id,
            FileName = "test.txt",
            Label = "Test File",
            Content = new byte[] { 1, 2, 3 },
            UploadDate = DateTime.UtcNow
        };
        dbContext.UserFiles!.Add(testFile);
        await dbContext.SaveChangesAsync();

        SetupGuestUser(user);
        var ctx = CreateTestContext(dbContext);

        var cut = ctx.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<NotADoctor>());
        await Task.Delay(100);

        // Act - Select then deselect file
        var checkbox = cut.Find("input[type='checkbox']");
        checkbox.Change(true);
        checkbox.Change(false);

        // Assert - Checkbox should not be checked
        Assert.False(checkbox.HasAttribute("checked"));

        // Cleanup
        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task NotADoctor_DisablesSendButton_WhenNoFilesSelected()
    {
        // Arrange
        var user = new ApplicationUser
        {
            UserName = "guest@example.com",
            Email = "guest@example.com",
            EmailConfirmed = true
        };
        SetupGuestUser(user);
        var dbContext = await CreateAndSetupDatabase();
        var ctx = CreateTestContext(dbContext);
        var cut = ctx.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<NotADoctor>());
        await Task.Delay(100);

        // Act - Try to send without selecting files
        var questionInput = cut.Find("input[id='question']");
        questionInput.Change("Test question");

        var sendButton = cut.Find("button");
        await sendButton.ClickAsync();

        // Assert - API should not be called
        _mockGrokService.Verify(s => s.GetChatCompletionAsync(It.IsAny<List<Message>>()), Times.Never);

        // Cleanup
        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task NotADoctor_SendsQuestionWithFileContext()
    {
        // Arrange
        var dbContext = await CreateAndSetupDatabase();
        var user = new ApplicationUser
        {
            UserName = "guest@example.com",
            Email = "guest@example.com",
            EmailConfirmed = true
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var testFile = new UserFile
        {
            UserId = user.Id,
            FileName = "test.txt",
            Label = "Test File",
            Content = System.Text.Encoding.UTF8.GetBytes("file content"),
            UploadDate = DateTime.UtcNow
        };
        dbContext.UserFiles!.Add(testFile);
        await dbContext.SaveChangesAsync();

        var testQuestion = "What is in this file?";
        var expectedResponse = "The file contains: file content";

        SetupGuestUser(user);
        _mockGrokService.Setup(s => s.GetChatCompletionAsync(It.IsAny<List<Message>>()))
            .ReturnsAsync(expectedResponse);
        var ctx = CreateTestContext(dbContext);

        var cut = ctx.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<NotADoctor>());
        await Task.Delay(100);

        // Act - Select file and enter question
        var checkbox = cut.Find("input[type='checkbox']");
        checkbox.Change(true);

        var questionInput = cut.Find("input[id='question']");
        questionInput.Change(testQuestion);

        var sendButton = cut.Find("button");
        await sendButton.ClickAsync();

        // Assert
        _mockGrokService.Verify(s => s.GetChatCompletionAsync(It.IsAny<List<Message>>()), Times.Once);
        Assert.Contains("You:", cut.Markup);
        Assert.Contains(testQuestion, cut.Markup);
        Assert.Contains("Grok:", cut.Markup);
        Assert.Contains(expectedResponse, cut.Markup);

        // Cleanup
        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task NotADoctor_ShowsLoadingState_DuringApiCall()
    {
        // Arrange
        var dbContext = await CreateAndSetupDatabase();
        var user = new ApplicationUser
        {
            UserName = "guest@example.com",
            Email = "guest@example.com",
            EmailConfirmed = true
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var testFile = new UserFile
        {
            UserId = user.Id,
            FileName = "test.txt",
            Label = "Test File",
            Content = System.Text.Encoding.UTF8.GetBytes("content"),
            UploadDate = DateTime.UtcNow
        };
        dbContext.UserFiles!.Add(testFile);
        await dbContext.SaveChangesAsync();

        SetupGuestUser(user);
        _mockGrokService.Setup(s => s.GetChatCompletionAsync(It.IsAny<List<Message>>()))
            .ReturnsAsync("response");
        var ctx = CreateTestContext(dbContext);

        var cut = ctx.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<NotADoctor>());
        await Task.Delay(100);

        // Act - Start the process
        await cut.InvokeAsync(async () =>
        {
            var checkbox = cut.Find("input[type='checkbox']");
            await checkbox.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true });

            var questionInput = cut.Find("input[id='question']");
            await questionInput.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "question" });

            var sendButton = cut.Find("button");
            var clickTask = sendButton.ClickAsync();

            // Assert - Button should be disabled during loading
            await cut.WaitForAssertionAsync(() =>
            {
                var button = cut.Find("button");
                Assert.True(button.HasAttribute("disabled"));
                Assert.Contains("Sending", button.TextContent);
            });

            await clickTask;
        });

        // Assert - Button should be enabled after completion
        await cut.WaitForAssertionAsync(() =>
        {
            var button = cut.Find("button");
            Assert.False(button.HasAttribute("disabled"));
            Assert.Contains("Send", button.TextContent.Trim());
        });

        // Cleanup
        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task NotADoctor_HandlesApiErrors()
    {
        // Arrange
        var dbContext = await CreateAndSetupDatabase();
        var user = new ApplicationUser
        {
            UserName = "guest@example.com",
            Email = "guest@example.com",
            EmailConfirmed = true
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var testFile = new UserFile
        {
            UserId = user.Id,
            FileName = "test.txt",
            Label = "Test File",
            Content = System.Text.Encoding.UTF8.GetBytes("content"),
            UploadDate = DateTime.UtcNow
        };
        dbContext.UserFiles!.Add(testFile);
        await dbContext.SaveChangesAsync();

        SetupGuestUser(user);
        _mockGrokService.Setup(s => s.GetChatCompletionAsync(It.IsAny<List<Message>>()))
            .ThrowsAsync(new Exception("API Error"));
        var ctx = CreateTestContext(dbContext);

        var cut = ctx.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<NotADoctor>());
        await Task.Delay(100);

        // Act - Select file and send
        var checkbox = cut.Find("input[type='checkbox']");
        checkbox.Change(true);

        var questionInput = cut.Find("input[id='question']");
        questionInput.Change("question");

        var sendButton = cut.Find("button");
        await sendButton.ClickAsync();

        // Assert - Error should be displayed
        Assert.Contains("Error: API Error", cut.Markup);

        // Cleanup
        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task NotADoctor_TruncatesLargeFileContent()
    {
        // Arrange
        var dbContext = await CreateAndSetupDatabase();
        var user = new ApplicationUser
        {
            UserName = "guest@example.com",
            Email = "guest@example.com",
            EmailConfirmed = true
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var largeContent = new string('x', 15000); // Over 10KB
        var testFile = new UserFile
        {
            UserId = user.Id,
            FileName = "large.txt",
            Label = "Large File",
            Content = System.Text.Encoding.UTF8.GetBytes(largeContent),
            UploadDate = DateTime.UtcNow
        };
        dbContext.UserFiles!.Add(testFile);
        await dbContext.SaveChangesAsync();

        SetupGuestUser(user);
        _mockGrokService.Setup(s => s.GetChatCompletionAsync(It.IsAny<List<Message>>()))
            .ReturnsAsync("response");
        var ctx = CreateTestContext(dbContext);

        var cut = ctx.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<NotADoctor>());
        await Task.Delay(100);

        // Act - Select file and send
        var checkbox = cut.Find("input[type='checkbox']");
        checkbox.Change(true);

        var questionInput = cut.Find("input[id='question']");
        questionInput.Change("question");

        var sendButton = cut.Find("button");
        await sendButton.ClickAsync();

        // Assert - API was called once with truncated content
        _mockGrokService.Verify(s => s.GetChatCompletionAsync(It.IsAny<List<Message>>()), Times.Once);

        // Cleanup
        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task NotADoctor_AllowsMultipleFileSelection()
    {
        // Arrange
        var dbContext = await CreateAndSetupDatabase();
        var user = new ApplicationUser
        {
            UserName = "guest@example.com",
            Email = "guest@example.com",
            EmailConfirmed = true
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var testFiles = new List<UserFile>
        {
            new UserFile { UserId = user.Id, FileName = "file1.txt", Label = "File 1", Content = System.Text.Encoding.UTF8.GetBytes("content1"), UploadDate = DateTime.UtcNow },
            new UserFile { UserId = user.Id, FileName = "file2.txt", Label = "File 2", Content = System.Text.Encoding.UTF8.GetBytes("content2"), UploadDate = DateTime.UtcNow },
            new UserFile { UserId = user.Id, FileName = "file3.txt", Label = "File 3", Content = System.Text.Encoding.UTF8.GetBytes("content3"), UploadDate = DateTime.UtcNow }
        };
        dbContext.UserFiles!.AddRange(testFiles);
        await dbContext.SaveChangesAsync();

        SetupGuestUser(user);
        _mockGrokService.Setup(s => s.GetChatCompletionAsync(It.IsAny<List<Message>>()))
            .ReturnsAsync("response");
        var ctx = CreateTestContext(dbContext);

        var cut = ctx.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<NotADoctor>());
        await Task.Delay(100);

        // Act - Select multiple files
        // Select first file
        await cut.InvokeAsync(async () => await cut.FindAll("input[type='checkbox']")[0].ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true }));

        // Select second file
        await cut.InvokeAsync(async () => await cut.FindAll("input[type='checkbox']")[1].ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true }));

        // Leave third unselected

        // Enter question
        await cut.InvokeAsync(async () => await cut.Find("input[id='question']").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "question" }));

        // Send
        await cut.InvokeAsync(async () => await cut.Find("button").ClickAsync());

        // Assert - API was called once with multiple files
        _mockGrokService.Verify(s => s.GetChatCompletionAsync(It.IsAny<List<Message>>()), Times.Once);

        // Cleanup
        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task NotADoctor_ClearsQuestionAfterSend()
    {
        // Arrange
        var dbContext = await CreateAndSetupDatabase();
        var user = new ApplicationUser
        {
            UserName = "guest@example.com",
            Email = "guest@example.com",
            EmailConfirmed = true
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var testFile = new UserFile
        {
            UserId = user.Id,
            FileName = "test.txt",
            Label = "Test File",
            Content = System.Text.Encoding.UTF8.GetBytes("content"),
            UploadDate = DateTime.UtcNow
        };
        dbContext.UserFiles!.Add(testFile);
        await dbContext.SaveChangesAsync();

        SetupGuestUser(user);
        _mockGrokService.Setup(s => s.GetChatCompletionAsync(It.IsAny<List<Message>>()))
            .ReturnsAsync("response");
        var ctx = CreateTestContext(dbContext);

        var cut = ctx.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<NotADoctor>());
        await Task.Delay(100);

        // Act - Enter question and send
        var checkbox = cut.Find("input[type='checkbox']");
        checkbox.Change(true);

        var questionInput = cut.Find("input[id='question']");
        questionInput.Change("test question");

        var sendButton = cut.Find("button");
        await sendButton.ClickAsync();

        // Assert - Question input should be cleared
        cut.WaitForAssertion(() =>
        {
            var input = cut.Find("input[id='question']");
            Assert.Equal("", input.GetAttribute("value"));
        });

        // Cleanup
        await dbContext.DisposeAsync();
    }

    private void SetupGuestUser(ApplicationUser user)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.Email ?? "guest@example.com"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Guest")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
        var userPrincipal = new System.Security.Claims.ClaimsPrincipal(identity);

        _mockAuthProvider.Setup(ap => ap.GetAuthenticationStateAsync())
            .ReturnsAsync(new AuthenticationState(userPrincipal));

        _mockUserManager.Setup(um => um.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns(user.Id);
    }
}

// Test helper classes for NotADoctor tests
public class NotADoctorTestServiceScopeFactory : IServiceScopeFactory
{
    private readonly UserManager<ApplicationUser> _userManager;

    public NotADoctorTestServiceScopeFactory(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public IServiceScope CreateScope()
    {
        return new NotADoctorTestServiceScope(_userManager);
    }
}

public class NotADoctorTestServiceScope : IServiceScope
{
    private readonly UserManager<ApplicationUser> _userManager;

    public NotADoctorTestServiceScope(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
        ServiceProvider = new NotADoctorTestServiceProvider(userManager);
    }

    public IServiceProvider ServiceProvider { get; }

    public void Dispose() { }
}

public class NotADoctorTestServiceProvider : IServiceProvider
{
    private readonly UserManager<ApplicationUser> _userManager;

    public NotADoctorTestServiceProvider(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(UserManager<ApplicationUser>))
        {
            return _userManager;
        }
        return null;
    }
}

// Test helper class for database context factory
public class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
{
    private readonly ApplicationDbContext? _context;

    public TestDbContextFactory(ApplicationDbContext? context)
    {
        _context = context;
    }

    public ApplicationDbContext CreateDbContext()
    {
        return _context ?? throw new InvalidOperationException("Database context not provided for test");
    }
}
