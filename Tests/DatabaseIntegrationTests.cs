using GrokBlazorApp.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Linq;

namespace GrokBlazorApp.Tests;

public class DatabaseIntegrationTests : IAsyncLifetime
{
    private ApplicationDbContext? _context;
    private DbContextOptions<ApplicationDbContext>? _options;

    public async Task InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new ApplicationDbContext(_options);
        await _context.Database.OpenConnectionAsync();
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            await _context.Database.CloseConnectionAsync();
            await _context.DisposeAsync();
        }
    }

    [Fact]
    public async Task UserFile_CRUD_Operations_WorkCorrectly()
    {
        Assert.NotNull(_context);
        // Arrange - Create user first for foreign key constraint
        var user = new ApplicationUser
        {
            UserName = "test@example.com",
            Email = "test@example.com",
            EmailConfirmed = true
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var userFile = new UserFile
        {
            UserId = user.Id,
            FileName = "test.txt",
            Label = "Test File",
            Content = [1, 2, 3, 4, 5],
            UploadDate = DateTime.UtcNow
        };

        // Act - Create
        _context.UserFiles!.Add(userFile);
        await _context.SaveChangesAsync();

        // Assert - Read
        var savedFile = await _context.UserFiles.FindAsync(userFile.Id);
        Assert.NotNull(savedFile);
        Assert.Equal(user.Id, savedFile.UserId);
        Assert.Equal("test.txt", savedFile.FileName);
        Assert.Equal("Test File", savedFile.Label);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, savedFile.Content);

        // Act - Update
        savedFile.Label = "Updated Label";
        await _context.SaveChangesAsync();

        // Assert - Update
        var updatedFile = await _context.UserFiles.FindAsync(userFile.Id);
        Assert.NotNull(updatedFile);
        Assert.Equal("Updated Label", updatedFile.Label);

        // Act - Delete
        _context.UserFiles.Remove(savedFile);
        await _context.SaveChangesAsync();

        // Assert - Delete
        var deletedFile = await _context.UserFiles.FindAsync(userFile.Id);
        Assert.Null(deletedFile);
    }

    [Fact]
    public async Task UserFile_OrderByUploadDate_ReturnsNewestFirst()
    {
        Assert.NotNull(_context);
        // Arrange - Create user first
        var user = new ApplicationUser
        {
            UserName = "test@example.com",
            Email = "test@example.com",
            EmailConfirmed = true
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var baseTime = DateTime.UtcNow;
        var files = new[]
        {
            new UserFile { UserId = user.Id, FileName = "old.txt", Label = "Old", Content = [1], UploadDate = baseTime.AddHours(-2) },
            new UserFile { UserId = user.Id, FileName = "new.txt", Label = "New", Content = [2], UploadDate = baseTime },
            new UserFile { UserId = user.Id, FileName = "middle.txt", Label = "Middle", Content = [3], UploadDate = baseTime.AddHours(-1) }
        };

        await _context.UserFiles!.AddRangeAsync(files);
        await _context.SaveChangesAsync();

        // Act
        var orderedFiles = await _context.UserFiles
            .Where(f => f.UserId == user.Id)
            .OrderByDescending(f => f.UploadDate)
            .ToListAsync();

        // Assert
        Assert.Equal(3, orderedFiles.Count);
        Assert.Equal("new.txt", orderedFiles[0].FileName);
        Assert.Equal("middle.txt", orderedFiles[1].FileName);
        Assert.Equal("old.txt", orderedFiles[2].FileName);
    }

    [Fact]
    public async Task UserFile_LargeFileContent_HandledCorrectly()
    {
        Assert.NotNull(_context);
        // Arrange - Create user first
        var user = new ApplicationUser
        {
            UserName = "test@example.com",
            Email = "test@example.com",
            EmailConfirmed = true
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var largeContent = new byte[1024 * 1024]; // 1MB
        for (int i = 0; i < largeContent.Length; i++)
        {
            largeContent[i] = (byte)(i % 256);
        }

        var userFile = new UserFile
        {
            UserId = user.Id,
            FileName = "large-file.dat",
            Label = "Large Test File",
            Content = largeContent,
            UploadDate = DateTime.UtcNow
        };

        // Act
        _context.UserFiles!.Add(userFile);
        await _context.SaveChangesAsync();

        // Assert
        var savedFile = await _context.UserFiles.FindAsync(userFile.Id);
        Assert.NotNull(savedFile);
        Assert.Equal(largeContent.Length, savedFile.Content.Length);
        Assert.Equal(largeContent, savedFile.Content);
    }

    [Fact]
    public async Task UserFile_QueryByUserId_ReturnsCorrectFiles()
    {
        Assert.NotNull(_context);
        // Arrange - Create users first
        var user1 = new ApplicationUser
        {
            UserName = "user1@example.com",
            Email = "user1@example.com",
            EmailConfirmed = true
        };
        var user2 = new ApplicationUser
        {
            UserName = "user2@example.com",
            Email = "user2@example.com",
            EmailConfirmed = true
        };
        _context.Users.AddRange(user1, user2);
        await _context.SaveChangesAsync();

        var files = new[]
        {
            new UserFile { UserId = user1.Id, FileName = "file1.txt", Label = "File 1", Content = [1], UploadDate = DateTime.UtcNow },
            new UserFile { UserId = user1.Id, FileName = "file2.txt", Label = "File 2", Content = [2], UploadDate = DateTime.UtcNow },
            new UserFile { UserId = user2.Id, FileName = "file3.txt", Label = "File 3", Content = [3], UploadDate = DateTime.UtcNow }
        };

        await _context.UserFiles!.AddRangeAsync(files);
        await _context.SaveChangesAsync();

        // Act
        var user1Files = await _context.UserFiles
            .Where(f => f.UserId == user1.Id)
            .OrderByDescending(f => f.UploadDate)
            .ToListAsync();

        // Assert
        Assert.Equal(2, user1Files.Count);
        Assert.Contains(user1Files, f => f.FileName == "file1.txt");
        Assert.Contains(user1Files, f => f.FileName == "file2.txt");
        Assert.DoesNotContain(user1Files, f => f.FileName == "file3.txt");
    }
}
