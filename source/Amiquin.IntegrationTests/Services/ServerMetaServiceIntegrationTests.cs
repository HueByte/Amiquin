using Amiquin.Core.Services.Meta;
using Amiquin.IntegrationTests.Fixtures;

namespace Amiquin.IntegrationTests.Services;

public class ServerMetaServiceIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;
    private readonly IServerMetaService _serverMetaService;

    public ServerMetaServiceIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _serverMetaService = _fixture.ServiceProvider.GetRequiredService<IServerMetaService>();
    }

    private async Task SeedServerData(ulong serverId)
    {
        var serverMeta = new Core.Models.ServerMeta
        {
            Id = serverId,
            ServerName = $"Test Server {serverId}",
            IsActive = true,
            Persona = "Test persona",
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
            Toggles = new List<Core.Models.Toggle>(),
            Messages = new List<Core.Models.Message>(),
            CommandLogs = new List<Core.Models.CommandLog>(),
            NachoPacks = new List<Core.Models.NachoPack>()
        };

        _fixture.DbContext.ServerMetas.Add(serverMeta);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task GetServerMetaAsync_WithExistingServer_ShouldReturnServerMeta()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var serverId = (ulong)Random.Shared.Next(100000000, 999999999);
        await SeedServerData(serverId);

        // Act
        var result = await _serverMetaService.GetServerMetaAsync(serverId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(serverId, result.Id);
        Assert.Equal($"Test Server {serverId}", result.ServerName);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetServerMetaAsync_WithNonExistentServer_ShouldReturnNull()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var Id = (ulong)Random.Shared.Next(100000000, 999999999);

        // Act
        var result = await _serverMetaService.GetServerMetaAsync(Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateServerMetaAsync_ShouldCreateAndReturnNewServerMeta()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var serverId = 987654321UL;
        var serverName = "New Test Server";

        // Act
        var result = await _serverMetaService.CreateServerMetaAsync(serverId, serverName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(serverId, result.Id);
        Assert.Equal(serverName, result.ServerName);
        Assert.True(result.IsActive);

        // Verify it was actually saved to database
        var savedServerMeta = await _serverMetaService.GetServerMetaAsync(serverId);
        Assert.NotNull(savedServerMeta);
        Assert.Equal(serverId, savedServerMeta.Id);
    }

    [Fact]
    public async Task UpdateServerMetaAsync_ShouldUpdateExistingServerMeta()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var Id = (ulong)Random.Shared.Next(100000000, 999999999);
        await SeedServerData(Id);
        var serverMeta = await _serverMetaService.GetServerMetaAsync(Id);
        Assert.NotNull(serverMeta);

        serverMeta.ServerName = "Updated Server Name";
        serverMeta.Persona = "Updated persona";

        // Act
        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

        // Assert
        var updatedServerMeta = await _serverMetaService.GetServerMetaAsync(Id);
        Assert.NotNull(updatedServerMeta);
        Assert.Equal("Updated Server Name", updatedServerMeta.ServerName);
        Assert.Equal("Updated persona", updatedServerMeta.Persona);
    }

    [Fact]
    public async Task DeleteServerMetaAsync_ShouldRemoveServerMeta()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var Id = (ulong)Random.Shared.Next(100000000, 999999999);
        await SeedServerData(Id);

        // Verify it exists first
        var serverMeta = await _serverMetaService.GetServerMetaAsync(Id);
        Assert.NotNull(serverMeta);

        // Act
        await _serverMetaService.DeleteServerMetaAsync(Id);

        // Assert
        var deletedServerMeta = await _serverMetaService.GetServerMetaAsync(Id);
        Assert.Null(deletedServerMeta);
    }

    [Fact]
    public async Task GetAllServerMetasAsync_ShouldReturnAllServerMetas()
    {
        // Arrange
        await _fixture.CleanupAsync();

        // Create multiple server metas
        await _serverMetaService.CreateServerMetaAsync(111111111UL, "Server 1");
        await _serverMetaService.CreateServerMetaAsync(222222222UL, "Server 2");
        await _serverMetaService.CreateServerMetaAsync(333333333UL, "Server 3");

        // Act
        var result = await _serverMetaService.GetAllServerMetasAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Contains(result, sm => sm.Id == 111111111UL);
        Assert.Contains(result, sm => sm.Id == 222222222UL);
        Assert.Contains(result, sm => sm.Id == 333333333UL);
    }

    [Fact]
    public async Task GetServerMetaAsync_WithIncludeToggles_ShouldLoadToggles()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var Id = (ulong)Random.Shared.Next(100000000, 999999999);
        await SeedServerData(Id);

        // Add some toggles first
        var toggleService = _fixture.ServiceProvider.GetRequiredService<Core.Services.Toggle.IToggleService>();
        await toggleService.SetServerToggleAsync(Id, "TestToggle1", true, "Description 1");
        await toggleService.SetServerToggleAsync(Id, "TestToggle2", false, "Description 2");

        // Act
        var result = await _serverMetaService.GetServerMetaAsync(Id, true);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Toggles);
        Assert.Equal(2, result.Toggles.Count);
        Assert.Contains(result.Toggles, t => t.Name == "TestToggle1" && t.IsEnabled);
        Assert.Contains(result.Toggles, t => t.Name == "TestToggle2" && !t.IsEnabled);
    }
}