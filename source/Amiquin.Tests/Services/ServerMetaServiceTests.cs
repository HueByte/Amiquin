using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Services.Meta;
using Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Amiquin.Tests.Services.ServerMeta;

public class ServerMetaServiceTests
{
    private readonly Mock<ILogger<IServerMetaService>> _loggerMock;
    private readonly Mock<IMemoryCache> _memoryCacheMock;
    private readonly Mock<IServerMetaRepository> _serverMetaRepositoryMock;
    private readonly ServerMetaService _sut; // System Under Test

    public ServerMetaServiceTests()
    {
        _loggerMock = new Mock<ILogger<IServerMetaService>>();

        _memoryCacheMock = new Mock<IMemoryCache>();
        var cacheEntryMock = new Mock<ICacheEntry>();
        _memoryCacheMock
            .Setup(m => m.CreateEntry(It.IsAny<object>()))
            .Returns(cacheEntryMock.Object);

        _serverMetaRepositoryMock = new Mock<IServerMetaRepository>();

        _sut = new ServerMetaService(
            _loggerMock.Object,
            _memoryCacheMock.Object,
            _serverMetaRepositoryMock.Object
        );
    }

    [Fact]
    public async Task GetServerMetaAsync_WithCachedData_ShouldReturnCachedData()
    {
        // Arrange
        var serverId = 123456789UL;
        var cacheKey = $"ServerMeta_{serverId}";
        var serverMeta = new Core.Models.ServerMeta { Id = serverId, IsActive = true };

        object? value = serverMeta;
        _memoryCacheMock.Setup(m => m.TryGetValue(cacheKey, out value))
            .Returns(true);

        // Act
        var result = await _sut.GetServerMetaAsync(serverId);

        // Assert
        Assert.Equal(serverMeta, result);
        _serverMetaRepositoryMock.Verify(r => r.AsQueryable(), Times.Never);
    }

    [Fact]
    public async Task GetServerMetaAsync_WithNoCachedData_ShouldFetchFromRepository()
    {
        // Arrange
        var serverId = 123456789UL;
        var cacheKey = $"ServerMeta_{serverId}";
        var serverMeta = new Core.Models.ServerMeta { Id = serverId, IsActive = true };

        object? value = null;
        _memoryCacheMock.Setup(m => m.TryGetValue(cacheKey, out value))
            .Returns(false);

        _serverMetaRepositoryMock.Setup(r => r.AsQueryable())
            .Returns(new List<Core.Models.ServerMeta> { serverMeta }.AsQueryable());

        // Act
        var result = await _sut.GetServerMetaAsync(serverId);

        // Assert
        Assert.Equal(serverMeta, result);
        _serverMetaRepositoryMock.Verify(r => r.AsQueryable(), Times.Once);
    }

    [Fact]
    public async Task GetServerMetaAsync_WithIncludeToggles_ShouldLoadToggles()
    {
        // Arrange
        var serverId = 123456789UL;
        var cacheKey = $"ServerMeta_{serverId}";
        var serverMeta = new Core.Models.ServerMeta
        {
            Id = serverId,
            IsActive = true,
            Toggles = new List<Core.Models.Toggle>()
        };

        object? value = serverMeta;
        _memoryCacheMock.Setup(m => m.TryGetValue(cacheKey, out value))
            .Returns(true);

        var toggles = new List<Core.Models.Toggle>
        {
            new Core.Models.Toggle { Id = "toggle1", ServerId = serverId, Name = "Toggle1", IsEnabled = true },
            new Core.Models.Toggle { Id = "toggle2", ServerId = serverId, Name = "Toggle2", IsEnabled = false }
        };

        _serverMetaRepositoryMock.Setup(r => r.AsQueryable())
            .Returns(new List<Core.Models.ServerMeta> { serverMeta }.AsQueryable());

        // Act
        var result = await _sut.GetServerMetaAsync(serverId, true);

        // Assert
        Assert.Equal(serverMeta, result);
        Assert.NotNull(result.Toggles);
        _serverMetaRepositoryMock.Verify(r => r.AsQueryable(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetOrCreateServerMetaAsync_WithExistingServerMeta_ShouldReturnExistingData()
    {
        // Arrange
        var Id = 123456789UL;
        var serverName = "Test Server";
        var cacheKey = $"ServerMeta_{Id}";
        var serverMeta = new Core.Models.ServerMeta
        {
            Id = Id,
            ServerName = serverName,
            IsActive = true
        };

        object? value = serverMeta;
        _memoryCacheMock.Setup(m => m.TryGetValue(cacheKey, out value))
            .Returns(true);

        var guildMock = new Mock<Discord.WebSocket.SocketGuild>();
        guildMock.Setup(g => g.Id).Returns(Id);
        guildMock.Setup(g => g.Name).Returns(serverName);

        var contextMock = new Mock<ExtendedShardedInteractionContext>();
        contextMock.Setup(c => c.Guild).Returns(guildMock.Object);

        // Act
        var result = await _sut.GetOrCreateServerMetaAsync(contextMock.Object);

        // Assert
        Assert.Equal(serverMeta, result);
        _serverMetaRepositoryMock.Verify(r => r.AsQueryable(), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateServerMetaAsync_WithNoExistingServerMeta_ShouldCreateNewServerMeta()
    {
        // Arrange
        var Id = 123456789UL;
        var serverName = "Test Server";
        var cacheKey = $"ServerMeta_{Id}";

        object? value = null;
        _memoryCacheMock.Setup(m => m.TryGetValue(cacheKey, out value))
            .Returns(false);

        _serverMetaRepositoryMock.Setup(r => r.AsQueryable())
            .Returns(new List<Core.Models.ServerMeta>().AsQueryable());

        var guildMock = new Mock<Discord.WebSocket.SocketGuild>();
        guildMock.Setup(g => g.Id).Returns(Id);
        guildMock.Setup(g => g.Name).Returns(serverName);

        var contextMock = new Mock<ExtendedShardedInteractionContext>();
        contextMock.Setup(c => c.Guild).Returns(guildMock.Object);

        // Act
        var result = await _sut.GetOrCreateServerMetaAsync(contextMock.Object);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Id, result.Id);
        Assert.Equal(serverName, result.ServerName);
        Assert.True(result.IsActive);
        _serverMetaRepositoryMock.Verify(r => r.AddAsync(It.Is<Core.Models.ServerMeta>(sm =>
            sm.Id == Id && sm.ServerName == serverName)), Times.Once);
        _serverMetaRepositoryMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteServerMetaAsync_ShouldRemoveServerMetaAndClearCache()
    {
        // Arrange
        var Id = 123456789UL;
        var serverMeta = new Core.Models.ServerMeta { Id = Id, IsActive = true };

        _serverMetaRepositoryMock.Setup(r => r.GetAsync(Id))
            .ReturnsAsync(serverMeta);
        _serverMetaRepositoryMock.Setup(r => r.RemoveAsync(serverMeta)).ReturnsAsync(true);

        // Act
        await _sut.DeleteServerMetaAsync(Id);

        // Assert
        _serverMetaRepositoryMock.Verify(r => r.RemoveAsync(serverMeta), Times.Once);
        _serverMetaRepositoryMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
