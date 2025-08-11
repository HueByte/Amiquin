using Amiquin.Core.IRepositories;
using Amiquin.Infrastructure.Repositories;
using Amiquin.IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Amiquin.IntegrationTests.Services;

public class UserStatsServiceIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;
    private readonly Mock<ILogger<UserStatsRepository>> _loggerMock;

    public UserStatsServiceIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _loggerMock = new Mock<ILogger<UserStatsRepository>>();
    }

    [Fact]
    public async Task GetOrCreateUserStatsAsync_ShouldCreateNewUserStats_WhenNotExists()
    {
        // Arrange
        var context = _fixture.DbContext;
        var repository = new UserStatsRepository(context, _loggerMock.Object);
        var userId = 123UL;
        var serverId = 456UL;

        // Act
        var userStats = await repository.GetOrCreateUserStatsAsync(userId, serverId);

        // Assert
        Assert.NotNull(userStats);
        Assert.Equal(userId, userStats.UserId);
        Assert.Equal(serverId, userStats.ServerId);
        Assert.True(userStats.Id > 0);
    }

    [Fact]
    public async Task GetOrCreateUserStatsAsync_ShouldReturnExisting_WhenExists()
    {
        // Arrange
        var context = _fixture.DbContext;
        var repository = new UserStatsRepository(context, _loggerMock.Object);
        var userId = 124UL;
        var serverId = 457UL;

        // Create first time
        var firstStats = await repository.GetOrCreateUserStatsAsync(userId, serverId);

        // Act
        var secondStats = await repository.GetOrCreateUserStatsAsync(userId, serverId);

        // Assert
        Assert.Equal(firstStats.Id, secondStats.Id);
    }

    [Fact]
    public async Task UserStats_StatMethods_ShouldWork()
    {
        // Arrange
        var context = _fixture.DbContext;
        var repository = new UserStatsRepository(context, _loggerMock.Object);
        var userId = 125UL;
        var serverId = 458UL;

        // Act
        var userStats = await repository.GetOrCreateUserStatsAsync(userId, serverId);
        
        // Test setting and getting stats
        userStats.SetStat("test_stat", 42);
        Assert.Equal(42, userStats.GetStat<int>("test_stat"));
        Assert.True(userStats.HasStat("test_stat"));
        
        // Test increment
        var newValue = userStats.IncrementStat("test_stat", 8);
        Assert.Equal(50, newValue);
        Assert.Equal(50, userStats.GetStat<int>("test_stat"));
        
        // Test default values
        Assert.Equal(0, userStats.GetStat<int>("nonexistent_stat"));
        Assert.Equal("default", userStats.GetStat("nonexistent_string", "default"));
        Assert.False(userStats.HasStat("nonexistent_stat"));

        // Save and verify persistence
        await repository.UpdateUserStatsAsync(userStats);
        
        // Get fresh instance and verify stats persist
        var freshStats = await repository.GetOrCreateUserStatsAsync(userId, serverId);
        Assert.Equal(50, freshStats.GetStat<int>("test_stat"));
    }

    [Fact]
    public async Task GetTopNachoGiversAsync_ShouldReturnCorrectResults()
    {
        // Arrange
        var context = _fixture.DbContext;
        var repository = new UserStatsRepository(context, _loggerMock.Object);
        var serverId = 459UL;

        // Create test data
        var user1 = await repository.GetOrCreateUserStatsAsync(111UL, serverId);
        user1.SetStat("nachos_given", 10);
        await repository.UpdateUserStatsAsync(user1);

        var user2 = await repository.GetOrCreateUserStatsAsync(222UL, serverId);
        user2.SetStat("nachos_given", 20);
        await repository.UpdateUserStatsAsync(user2);

        var user3 = await repository.GetOrCreateUserStatsAsync(333UL, serverId);
        user3.SetStat("nachos_given", 5);
        await repository.UpdateUserStatsAsync(user3);

        // Act
        var topGivers = await repository.GetTopNachoGiversAsync(serverId, 2);

        // Assert
        Assert.Equal(2, topGivers.Count);
        Assert.Equal(222UL, topGivers[0].UserId); // Highest giver
        Assert.Equal(111UL, topGivers[1].UserId); // Second highest
    }

    [Fact]
    public async Task GetTotalNachosReceivedAsync_ShouldReturnCorrectTotal()
    {
        // Arrange
        var context = _fixture.DbContext;
        var repository = new UserStatsRepository(context, _loggerMock.Object);
        var serverId = 460UL;

        // Create test data
        var user1 = await repository.GetOrCreateUserStatsAsync(1111UL, serverId);
        user1.SetStat("nachos_given", 10);
        await repository.UpdateUserStatsAsync(user1);

        var user2 = await repository.GetOrCreateUserStatsAsync(2222UL, serverId);
        user2.SetStat("nachos_given", 15);
        await repository.UpdateUserStatsAsync(user2);

        // Act
        var total = await repository.GetTotalNachosReceivedAsync(serverId);

        // Assert
        Assert.Equal(25, total);
    }

    [Fact]
    public async Task UserStats_StringAndComplexTypes_ShouldWork()
    {
        // Arrange
        var context = _fixture.DbContext;
        var repository = new UserStatsRepository(context, _loggerMock.Object);
        var userId = 126UL;
        var serverId = 461UL;

        // Act
        var userStats = await repository.GetOrCreateUserStatsAsync(userId, serverId);
        
        // Test string stats
        userStats.SetStat("username", "TestUser123");
        Assert.Equal("TestUser123", userStats.GetStat<string>("username"));
        
        // Test boolean stats
        userStats.SetStat("is_premium", true);
        Assert.True(userStats.GetStat<bool>("is_premium"));
        
        // Test decimal stats
        userStats.SetStat("score", 123.45);
        Assert.Equal(123.45, userStats.GetStat<double>("score"), 2);

        // Save and verify persistence
        await repository.UpdateUserStatsAsync(userStats);
        
        // Get fresh instance and verify all types persist
        var freshStats = await repository.GetOrCreateUserStatsAsync(userId, serverId);
        Assert.Equal("TestUser123", freshStats.GetStat<string>("username"));
        Assert.True(freshStats.GetStat<bool>("is_premium"));
        Assert.Equal(123.45, freshStats.GetStat<double>("score"), 2);
    }
}