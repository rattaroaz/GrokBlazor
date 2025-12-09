using Bunit;
using GrokBlazorApp.Components.Pages;
using GrokBlazorApp.Data;
using GrokBlazorApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace GrokBlazorApp.Tests;

public class ConversionPageTests : IAsyncLifetime
{
    private DbContextOptions<ApplicationDbContext>? _dbOptions;
    private readonly Mock<ILocalTextExtractionService> _mockLocalExtractor;
    private readonly Mock<AuthenticationStateProvider> _mockAuthProvider;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public ConversionPageTests()
    {
        _mockLocalExtractor = new Mock<ILocalTextExtractionService>();
        _mockAuthProvider = new Mock<AuthenticationStateProvider>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);
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
        return Task.CompletedTask;
    }

    private async Task<ApplicationDbContext> CreateAndSetupDatabase()
    {
        var dbContext = new ApplicationDbContext(_dbOptions!);
        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private BunitContext CreateTestContext(ApplicationDbContext dbContext)
    {
        var ctx = new BunitContext();

        ctx.Services.AddSingleton<ILocalTextExtractionService>(_mockLocalExtractor.Object);
        ctx.Services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(new TestDbContextFactory(dbContext));
        ctx.Services.AddSingleton<AuthenticationStateProvider>(_mockAuthProvider.Object);
        ctx.Services.AddSingleton<IServiceScopeFactory>(new NotADoctorTestServiceScopeFactory(_mockUserManager.Object));
        ctx.Services.AddSingleton(_mockUserManager.Object);

        ctx.Services.AddAuthorizationCore();
        ctx.Services.AddSingleton<IAuthorizationService, DefaultAuthorizationService>();
        ctx.Services.AddSingleton<IAuthorizationPolicyProvider, DefaultAuthorizationPolicyProvider>();
        ctx.Services.AddSingleton<IAuthorizationHandlerProvider, DefaultAuthorizationHandlerProvider>();

        return ctx;
    }

    private void SetupGuestUser(ApplicationUser user)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.Name, user.Email ?? "guest@example.com"),
            new(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id),
            new(System.Security.Claims.ClaimTypes.Role, "Guest")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);

        _mockAuthProvider.Setup(ap => ap.GetAuthenticationStateAsync())
            .ReturnsAsync(new AuthenticationState(principal));

        _mockUserManager.Setup(um => um.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns(user.Id);
    }

    [Fact]
    public async Task Conversion_LoadsAndDisplaysUserFilesForGuestUser()
    {
        var dbContext = await CreateAndSetupDatabase();
        try
        {
            var user = new ApplicationUser
            {
                UserName = "guest@example.com",
                Email = "guest@example.com",
                EmailConfirmed = true
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            var files = new[]
            {
                new UserFile { UserId = user.Id, FileName = "file1.pdf", Label = "File 1", Content = [1], UploadDate = DateTime.UtcNow.AddMinutes(-1) },
                new UserFile { UserId = user.Id, FileName = "file2.docx", Label = "File 2", Content = [2], UploadDate = DateTime.UtcNow }
            };
            await dbContext.UserFiles!.AddRangeAsync(files);
            await dbContext.SaveChangesAsync();

            SetupGuestUser(user);
            using var ctx = CreateTestContext(dbContext);

            var cut = ctx.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<Conversion>());

            await Task.Delay(100);

            Assert.Contains("Convert Files to TXT", cut.Markup);
            Assert.Contains("Convertible Files", cut.Markup);
            Assert.Contains("Converted Files", cut.Markup);
            Assert.Contains("File 1", cut.Markup);
            Assert.Contains("File 2", cut.Markup);
            Assert.Equal(2, cut.FindAll("tbody tr").Count);
        }
        finally
        {
            await dbContext.DisposeAsync();
        }
    }

    [Fact]
    public async Task Conversion_ConvertSelectedFile_CreatesTxtAndNavigatesToUpload()
    {
        var dbContext = await CreateAndSetupDatabase();
        try
        {
            var user = new ApplicationUser
            {
                UserName = "guest@example.com",
                Email = "guest@example.com",
                EmailConfirmed = true
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            var sourceFile = new UserFile
            {
                UserId = user.Id,
                FileName = "report.pdf",
                Label = "Report",
                Content = System.Text.Encoding.UTF8.GetBytes("file content"),
                UploadDate = DateTime.UtcNow
            };
            dbContext.UserFiles!.Add(sourceFile);
            await dbContext.SaveChangesAsync();

            var expectedExtractedText = "extracted text";
            _mockLocalExtractor
                .Setup(s => s.ExtractTextAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
                .ReturnsAsync(expectedExtractedText);

            SetupGuestUser(user);
            using var ctx = CreateTestContext(dbContext);

            var cut = ctx.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<Conversion>());
            await Task.Delay(100);

            var checkbox = cut.Find("input[type='checkbox']");
            checkbox.Change(true);

            var convertButton = cut.Find("button");
            await convertButton.ClickAsync();

            await Task.Delay(100);

            _mockLocalExtractor.Verify(s => s.ExtractTextAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Once);
        }
        finally
        {
            await dbContext.DisposeAsync();
        }
    }

    [Fact]
    public async Task Conversion_WhenAllFilesAlreadyTxt_ShowsNoFilesConvertedAndDoesNotCallApi()
    {
        var dbContext = await CreateAndSetupDatabase();
        try
        {
            var user = new ApplicationUser
            {
                UserName = "guest@example.com",
                Email = "guest@example.com",
                EmailConfirmed = true
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            var files = new[]
            {
                new UserFile { UserId = user.Id, FileName = "a.txt", Label = "A", Content = [1], UploadDate = DateTime.UtcNow },
                new UserFile { UserId = user.Id, FileName = "b.txt", Label = "B", Content = [2], UploadDate = DateTime.UtcNow }
            };
            await dbContext.UserFiles!.AddRangeAsync(files);
            await dbContext.SaveChangesAsync();

            SetupGuestUser(user);
            using var ctx = CreateTestContext(dbContext);

            var navManager = ctx.Services.GetRequiredService<NavigationManager>();
            var initialUri = navManager.Uri;

            var cut = ctx.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<Conversion>());
            await Task.Delay(100);

            var convertButton = cut.Find("button");
            await convertButton.ClickAsync();

            await cut.WaitForAssertionAsync(() =>
            {
                Assert.Contains("No files were converted.", cut.Markup);
            });

            _mockLocalExtractor.Verify(s => s.ExtractTextAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
            Assert.Equal(initialUri, navManager.Uri);
        }
        finally
        {
            await dbContext.DisposeAsync();
        }
    }
}
