using Bunit;
using Bunit.TestDoubles;
using GrokBlazorApp.Components.Pages;
using GrokBlazorApp.Data;
using GrokBlazorApp.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Compliance.Redaction;

namespace GrokBlazorApp.Tests;

public class AnonymizerTests : IAsyncLifetime
{
    private readonly Mock<AuthenticationStateProvider> _mockAuthProvider;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IRedactorProvider> _mockRedactorProvider;
    private readonly Mock<IAnonymizerService> _mockAnonymizerService;

    private DbContextOptions<ApplicationDbContext>? _dbOptions;

    public AnonymizerTests()
    {
        _mockAuthProvider = new Mock<AuthenticationStateProvider>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockRedactorProvider = new Mock<IRedactorProvider>();
        _mockAnonymizerService = new Mock<IAnonymizerService>();
        
        // Default setup for AnonymizerService to return the input text (no-op mock by default)
        _mockAnonymizerService.Setup(s => s.AnonymizeText(It.IsAny<string>()))
            .Returns((string s) => s);
    }

    public async Task InitializeAsync()
    {
        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        await Task.Yield();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private BunitContext CreateTestContext(ApplicationDbContext? dbContext = null)
    {
        var ctx = new BunitContext();

        ctx.Services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(new TestDbContextFactory(dbContext));
        ctx.Services.AddSingleton<AuthenticationStateProvider>(_mockAuthProvider.Object);
        ctx.Services.AddSingleton<IServiceScopeFactory>(new NotADoctorTestServiceScopeFactory(_mockUserManager.Object));
        ctx.Services.AddSingleton(_mockUserManager.Object);
        ctx.Services.AddSingleton<IRedactorProvider>(_mockRedactorProvider.Object);
        ctx.Services.AddSingleton<IAnonymizerService>(_mockAnonymizerService.Object);

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

    private void SetupGuestUser(ApplicationUser user)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.Email ?? "guest@example.com"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Guest")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);

        _mockAuthProvider.Setup(ap => ap.GetAuthenticationStateAsync())
            .ReturnsAsync(new AuthenticationState(principal));

        _mockUserManager.Setup(um => um.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns(user.Id);
    }

    [Fact]
    public async Task Anonymizer_LoadsAndDisplaysUserTxtFiles()
    {
        var dbContext = await CreateAndSetupDatabase();
        var user = new ApplicationUser
        {
            UserName = "guest@example.com",
            Email = "guest@example.com",
            EmailConfirmed = true
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var txtFiles = new[]
        {
            new UserFile { UserId = user.Id, FileName = "test.txt", Label = "Test", Content = System.Text.Encoding.UTF8.GetBytes("content"), UploadDate = DateTime.UtcNow },
            new UserFile { UserId = user.Id, FileName = "AN-test.txt", Label = "Anonymized", Content = System.Text.Encoding.UTF8.GetBytes("anon"), UploadDate = DateTime.UtcNow }
        };
        await dbContext.UserFiles!.AddRangeAsync(txtFiles);
        await dbContext.SaveChangesAsync();

        SetupGuestUser(user);
        using var ctx = CreateTestContext(dbContext);

        var cut = ctx.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<Anonymizer>());

        await Task.Delay(100);

        Assert.Contains("Anonymize TXT Files", cut.Markup);
        Assert.Contains("Original TXT Files", cut.Markup);
        Assert.Contains("Anonymized TXT Files", cut.Markup);
        Assert.Contains("Test", cut.Markup);
        Assert.Contains("Anonymized", cut.Markup);
    }

    [Fact]
    public async Task Anonymizer_HandlesEmptyFileContent()
    {
        var dbContext = await CreateAndSetupDatabase();
        var user = new ApplicationUser
        {
            UserName = "guest@example.com",
            Email = "guest@example.com",
            EmailConfirmed = true
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var emptyFile = new UserFile
        {
            UserId = user.Id,
            FileName = "empty.txt",
            Label = "Empty",
            Content = new byte[0],
            UploadDate = DateTime.UtcNow
        };
        dbContext.UserFiles!.Add(emptyFile);
        await dbContext.SaveChangesAsync();

        SetupGuestUser(user);
        using var ctx = CreateTestContext(dbContext);

        var cut = ctx.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<Anonymizer>());
        await Task.Delay(100);

        var checkbox = cut.Find("input[type='checkbox']");
        checkbox.Change(true);

        var button = cut.Find("button");
        await button.ClickAsync();

        await cut.WaitForAssertionAsync(() =>
        {
            Assert.Contains("No files were anonymized.", cut.Markup);
        });
    }

    [Fact]
    public void Anonymizer_AnonymizeText_RemovesSensitiveInfo()
    {
        // Create a RedactorProvider with proper configuration
        var services = new ServiceCollection();
        services.AddRedaction(options =>
        {
            options.SetRedactor<PiiRedactor>(DataTaxonomy.PiiData);
            options.SetRedactor<FinancialRedactor>(DataTaxonomy.FinancialData);
            options.SetRedactor<MedicalRedactor>(DataTaxonomy.MedicalData);
            options.SetRedactor<SensitiveDataRedactor>(DataTaxonomy.SensitiveData);
        });
        var serviceProvider = services.BuildServiceProvider();
        var redactorProvider = serviceProvider.GetRequiredService<IRedactorProvider>();
        
        var service = new AnonymizerService(redactorProvider);
        var text = "My email is test@example.com and phone is 123-456-7890, SSN 123-45-6789.";

        var result = service.AnonymizeText(text);

        // Verify sensitive data is redacted (email, phone, SSN are all PII)
        Assert.DoesNotContain("test@example.com", result);
        Assert.DoesNotContain("123-456-7890", result);
        Assert.DoesNotContain("123-45-6789", result);
        Assert.Contains("[REDACTED]", result);
    }
}
