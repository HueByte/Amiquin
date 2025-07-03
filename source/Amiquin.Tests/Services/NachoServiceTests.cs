using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Amiquin.Core.Services.Nacho;
using MockQueryable;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace Amiquin.Tests.Services;

public class NachoServiceTests
{
    private readonly Mock<INachoRepository> _nachoRepositoryMock;
    private readonly NachoService _sut; // System Under Test

    public NachoServiceTests()
    {
        _nachoRepositoryMock = new Mock<INachoRepository>();
        _sut = new NachoService(_nachoRepositoryMock.Object);
    }

    [Fact]
    public async Task GetUserNachoCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var userId = 123456789UL;
        var nachoPacks = new List<NachoPack>
        {
            new NachoPack { Id = 1, UserId = userId, ServerId = 111UL, NachoCount = 3 },
            new NachoPack { Id = 2, UserId = userId, ServerId = 222UL, NachoCount = 2 },
            new NachoPack { Id = 3, UserId = 999, ServerId = 111UL, NachoCount = 5 } // Different user
        };

        var mock = nachoPacks.AsQueryable().BuildMock();
        _nachoRepositoryMock.Setup(x => x.AsQueryable())
            .Returns(mock);

        // Act
        var result = await _sut.GetUserNachoCountAsync(userId);

        // Assert
        Assert.Equal(5, result); // 3 + 2
    }

    [Fact]
    public async Task GetServerNachoCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var serverId = 111UL;
        var nachoPacks = new List<NachoPack>
        {
            new NachoPack { Id = 1, UserId = 123, ServerId = serverId, NachoCount = 3 },
            new NachoPack { Id = 2, UserId = 456, ServerId = serverId, NachoCount = 2 },
            new NachoPack { Id = 3, UserId = 789, ServerId = 222UL, NachoCount = 5 } // Different server
        };

        var mock = nachoPacks.AsQueryable().BuildMock();
        _nachoRepositoryMock.Setup(x => x.AsQueryable())
            .Returns(mock);

        // Act
        var result = await _sut.GetServerNachoCountAsync(serverId);

        // Assert
        Assert.Equal(5, result); // 3 + 2
    }

    [Fact]
    public async Task GetTotalNachoCountAsync_ShouldReturnTotalCount()
    {
        // Arrange
        var nachoPacks = new List<NachoPack>
        {
            new NachoPack { Id = 1, UserId = 123, ServerId = 111UL, NachoCount = 3 },
            new NachoPack { Id = 2, UserId = 456, ServerId = 222UL, NachoCount = 2 },
            new NachoPack { Id = 3, UserId = 789, ServerId = 333UL, NachoCount = 5 }
        };

        var mock = nachoPacks.AsQueryable().BuildMock();
        _nachoRepositoryMock.Setup(x => x.AsQueryable())
            .Returns(mock);

        // Act
        var result = await _sut.GetTotalNachoCountAsync();

        // Assert
        Assert.Equal(10, result); // 3 + 2 + 5
    }

    [Fact]
    public async Task AddNachoAsync_WithValidInput_ShouldAddNacho()
    {
        // Arrange
        var userId = 123456789UL;
        var serverId = 987654321UL;
        var nachoCount = 2;
        var today = DateTime.UtcNow.Date;

        var existingNachos = new List<NachoPack>
        {
            new NachoPack { UserId = userId, NachoCount = 1, NachoReceivedDate = today }
        };

        var mock = existingNachos.AsQueryable().BuildMock();
        _nachoRepositoryMock.Setup(x => x.AsQueryable())
            .Returns(mock);
        _nachoRepositoryMock.Setup(x => x.AddAsync(It.IsAny<NachoPack>()))
            .Returns(Task.FromResult(true));
        _nachoRepositoryMock.Setup(x => x.SaveChangesAsync())
            .Returns(Task.FromResult(true));

        // Act
        await _sut.AddNachoAsync(userId, serverId, nachoCount);

        // Assert
        _nachoRepositoryMock.Verify(x => x.AddAsync(It.Is<NachoPack>(n =>
            n.UserId == userId &&
            n.ServerId == serverId &&
            n.NachoCount == nachoCount &&
            n.NachoReceivedDate.Date == today
        )), Times.Once);
        _nachoRepositoryMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AddNachoAsync_WithZeroCount_ShouldThrowException()
    {
        // Arrange
        var userId = 123456789UL;
        var serverId = 987654321UL;
        var nachoCount = 0;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _sut.AddNachoAsync(userId, serverId, nachoCount));

        Assert.Equal("Hey, that's not cool. At least give 1 nacho.", exception.Message);
    }

    [Fact]
    public async Task AddNachoAsync_ExceedingDailyLimit_ShouldThrowException()
    {
        // Arrange
        var userId = 123456789UL;
        var serverId = 987654321UL;
        var nachoCount = 3;
        var today = DateTime.UtcNow.Date;

        var existingNachos = new List<NachoPack>
        {
            new NachoPack { UserId = userId, NachoCount = 3, NachoReceivedDate = today },
            new NachoPack { UserId = userId, NachoCount = 1, NachoReceivedDate = today }
        };

        var mock = existingNachos.AsQueryable().BuildMock();
        _nachoRepositoryMock.Setup(x => x.AsQueryable())
            .Returns(mock);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _sut.AddNachoAsync(userId, serverId, nachoCount));

        Assert.Equal("You can only give 5 nachos per day.", exception.Message);
    }

    [Fact]
    public async Task AddNachoAsync_WithPreviousDayNachos_ShouldNotCountTowardsLimit()
    {
        // Arrange
        var userId = 123456789UL;
        var serverId = 987654321UL;
        var nachoCount = 2;
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        var existingNachos = new List<NachoPack>
        {
            new NachoPack { UserId = userId, NachoCount = 5, NachoReceivedDate = yesterday }, // Previous day
            new NachoPack { UserId = userId, NachoCount = 1, NachoReceivedDate = today }      // Today
        };

        var mock = existingNachos.AsQueryable().BuildMock();
        _nachoRepositoryMock.Setup(x => x.AsQueryable())
            .Returns(mock);
        _nachoRepositoryMock.Setup(x => x.AddAsync(It.IsAny<NachoPack>()))
            .Returns(Task.FromResult(true));
        _nachoRepositoryMock.Setup(x => x.SaveChangesAsync())
            .Returns(Task.FromResult(true));

        // Act
        await _sut.AddNachoAsync(userId, serverId, nachoCount);

        // Assert
        _nachoRepositoryMock.Verify(x => x.AddAsync(It.IsAny<NachoPack>()), Times.Once);
        _nachoRepositoryMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RemoveNachoAsync_WithExistingNacho_ShouldRemoveNacho()
    {
        // Arrange
        var userId = 123456789UL;
        var serverId = 987654321UL;
        var existingNacho = new NachoPack
        {
            Id = 1,
            UserId = userId,
            ServerId = serverId,
            NachoCount = 3
        };

        var mock = new List<NachoPack> { existingNacho }.AsQueryable().BuildMock();
        _nachoRepositoryMock.Setup(x => x.AsQueryable())
            .Returns(mock);
        _nachoRepositoryMock.Setup(x => x.RemoveAsync(It.IsAny<NachoPack>()))
            .Returns(Task.FromResult(true));
        _nachoRepositoryMock.Setup(x => x.SaveChangesAsync())
            .Returns(Task.FromResult(true));

        // Act
        await _sut.RemoveNachoAsync(userId, serverId);

        // Assert
        _nachoRepositoryMock.Verify(x => x.RemoveAsync(existingNacho), Times.Once);
        _nachoRepositoryMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RemoveNachoAsync_WithNonExistingNacho_ShouldNotCallRemove()
    {
        // Arrange
        var userId = 123456789UL;
        var serverId = 987654321UL;

        var mock = new List<NachoPack>().AsQueryable().BuildMock();
        _nachoRepositoryMock.Setup(x => x.AsQueryable())
            .Returns(mock);

        // Act
        await _sut.RemoveNachoAsync(userId, serverId);

        // Assert
        _nachoRepositoryMock.Verify(x => x.RemoveAsync(It.IsAny<NachoPack>()), Times.Never);
        _nachoRepositoryMock.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task RemoveAllNachoAsync_WithExistingNachos_ShouldRemoveAll()
    {
        // Arrange
        var userId = 123456789UL;
        var nachosToRemove = new List<NachoPack>
        {
            new NachoPack { Id = 1, UserId = userId, ServerId = 111UL, NachoCount = 3 },
            new NachoPack { Id = 2, UserId = userId, ServerId = 222UL, NachoCount = 2 }
        };

        var mock = nachosToRemove.AsQueryable().BuildMock();
        _nachoRepositoryMock.Setup(x => x.AsQueryable())
            .Returns(mock);
        _nachoRepositoryMock.Setup(x => x.RemoveRangeAsync(It.IsAny<IEnumerable<NachoPack>>()))
            .Returns(Task.FromResult(true));
        _nachoRepositoryMock.Setup(x => x.SaveChangesAsync())
            .Returns(Task.FromResult(true));

        // Act
        await _sut.RemoveAllNachoAsync(userId);

        // Assert
        _nachoRepositoryMock.Verify(x => x.RemoveRangeAsync(nachosToRemove), Times.Once);
        _nachoRepositoryMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RemoveAllServerNachoAsync_WithExistingNachos_ShouldRemoveAll()
    {
        // Arrange
        var serverId = 987654321UL;
        var nachosToRemove = new List<NachoPack>
        {
            new NachoPack { Id = 1, UserId = 123, ServerId = serverId, NachoCount = 3 },
            new NachoPack { Id = 2, UserId = 456, ServerId = serverId, NachoCount = 2 }
        };

        var mock = nachosToRemove.AsQueryable().BuildMock();
        _nachoRepositoryMock.Setup(x => x.AsQueryable())
            .Returns(mock);
        _nachoRepositoryMock.Setup(x => x.RemoveRangeAsync(It.IsAny<IEnumerable<NachoPack>>()))
            .Returns(Task.FromResult(true));
        _nachoRepositoryMock.Setup(x => x.SaveChangesAsync())
            .Returns(Task.FromResult(true));

        // Act
        await _sut.RemoveAllServerNachoAsync(serverId);

        // Assert
        _nachoRepositoryMock.Verify(x => x.RemoveRangeAsync(nachosToRemove), Times.Once);
        _nachoRepositoryMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }
}