using Amiquin.Core.Services.Meta;
using Amiquin.Core.Services.Toggle;
using Amiquin.IntegrationTests.Fixtures;

namespace Amiquin.IntegrationTests.Services;

public class ToggleServiceIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;
    private readonly IToggleService _toggleService;
    private readonly IServerMetaService _serverMetaService;

    public ToggleServiceIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _toggleService = _fixture.ServiceProvider.GetRequiredService<IToggleService>();
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
    public async Task SetServerToggleAsync_WithNewToggle_ShouldCreateToggle()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var serverId = (ulong)Random.Shared.Next(100000000, 999999999);
        await SeedServerData(serverId);
        var toggleName = "NewToggle";
        var isEnabled = true;
        var description = "New toggle description";

        // Act
        await _toggleService.SetServerToggleAsync(serverId, toggleName, isEnabled, description);

        // Assert
        var result = await _toggleService.IsEnabledAsync(serverId, toggleName);
        Assert.True(result);

        var toggles = await _toggleService.GetTogglesByServerId(serverId);
        Assert.NotNull(toggles);
        Assert.Contains(toggles, t => t.Name == toggleName && t.IsEnabled == isEnabled && t.Description == description);
    }

    [Fact]
    public async Task SetServerToggleAsync_WithExistingToggle_ShouldUpdateToggle()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var serverId = (ulong)Random.Shared.Next(100000000, 999999999);
        await SeedServerData(serverId);
        var toggleName = "ExistingToggle";

        // Create initial toggle
        await _toggleService.SetServerToggleAsync(serverId, toggleName, true, "Original description");
        var initialState = await _toggleService.IsEnabledAsync(serverId, toggleName);
        Assert.True(initialState);

        // Act - Update the toggle
        await _toggleService.SetServerToggleAsync(serverId, toggleName, false, "Updated description");

        // Assert
        var updatedState = await _toggleService.IsEnabledAsync(serverId, toggleName);
        Assert.False(updatedState);

        var toggles = await _toggleService.GetTogglesByServerId(serverId);
        var updatedToggle = toggles.FirstOrDefault(t => t.Name == toggleName);
        Assert.NotNull(updatedToggle);
        Assert.False(updatedToggle.IsEnabled);
        Assert.Equal("Updated description", updatedToggle.Description);
    }

    [Fact]
    public async Task IsEnabledAsync_WithActiveServerAndEnabledToggle_ShouldReturnTrue()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var serverId = (ulong)Random.Shared.Next(100000000, 999999999);
        await SeedServerData(serverId);
        var toggleName = "EnabledToggle";

        await _toggleService.SetServerToggleAsync(serverId, toggleName, true, "Enabled toggle");

        // Act
        var result = await _toggleService.IsEnabledAsync(serverId, toggleName);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsEnabledAsync_WithActiveServerAndDisabledToggle_ShouldReturnFalse()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var serverId = (ulong)Random.Shared.Next(100000000, 999999999);
        await SeedServerData(serverId);
        var toggleName = "DisabledToggle";

        await _toggleService.SetServerToggleAsync(serverId, toggleName, false, "Disabled toggle");

        // Act
        var result = await _toggleService.IsEnabledAsync(serverId, toggleName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsEnabledAsync_WithInactiveServer_ShouldReturnFalse()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var serverId = (ulong)Random.Shared.Next(100000000, 999999999);
        var toggleName = "TestToggle";

        // Create server but set it as inactive
        var serverMeta = await _serverMetaService.CreateServerMetaAsync(serverId, "Test Server");
        serverMeta.IsActive = false;
        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

        await _toggleService.SetServerToggleAsync(serverId, toggleName, true, "Test toggle");

        // Act
        var result = await _toggleService.IsEnabledAsync(serverId, toggleName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsEnabledAsync_WithNonExistentServer_ShouldReturnTrue()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var serverId = (ulong)Random.Shared.Next(100000000, 999999999);
        var toggleName = "NonExistentToggle";

        // Act
        var result = await _toggleService.IsEnabledAsync(serverId, toggleName);

        // Assert
        Assert.True(result); // Default behavior for non-existent servers
    }

    [Fact]
    public async Task GetTogglesByServerId_ShouldReturnAllServerToggles()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var serverId = (ulong)Random.Shared.Next(100000000, 999999999);
        await SeedServerData(serverId);

        // Create multiple toggles
        await _toggleService.SetServerToggleAsync(serverId, "Toggle1", true, "Description 1");
        await _toggleService.SetServerToggleAsync(serverId, "Toggle2", false, "Description 2");
        await _toggleService.SetServerToggleAsync(serverId, "Toggle3", true, "Description 3");

        // Act
        var result = await _toggleService.GetTogglesByServerId(serverId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Contains(result, t => t.Name == "Toggle1" && t.IsEnabled);
        Assert.Contains(result, t => t.Name == "Toggle2" && !t.IsEnabled);
        Assert.Contains(result, t => t.Name == "Toggle3" && t.IsEnabled);
    }

    [Fact]
    public async Task SetServerTogglesBulkAsync_ShouldCreateMultipleToggles()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var serverId = (ulong)Random.Shared.Next(100000000, 999999999);
        await SeedServerData(serverId);
        var toggles = new Dictionary<string, (bool IsEnabled, string? Description)>
        {
            { "BulkToggle1", (true, "Bulk description 1") },
            { "BulkToggle2", (false, "Bulk description 2") },
            { "BulkToggle3", (true, "Bulk description 3") }
        };

        // Act
        await _toggleService.SetServerTogglesBulkAsync(serverId, toggles);

        // Assert
        var result = await _toggleService.GetTogglesByServerId(serverId);
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);

        foreach (var expectedToggle in toggles)
        {
            var actualToggle = result.FirstOrDefault(t => t.Name == expectedToggle.Key);
            Assert.NotNull(actualToggle);
            Assert.Equal(expectedToggle.Value.IsEnabled, actualToggle.IsEnabled);
            Assert.Equal(expectedToggle.Value.Description, actualToggle.Description);
        }
    }

    [Fact]
    public async Task CreateServerTogglesIfNotExistsAsync_WithExistingToggles_ShouldNotDuplicate()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var serverId = (ulong)Random.Shared.Next(100000000, 999999999);
        await SeedServerData(serverId);

        // Create some initial toggles
        await _toggleService.SetServerToggleAsync(serverId, "ExistingToggle1", true, "Description 1");
        await _toggleService.SetServerToggleAsync(serverId, "ExistingToggle2", false, "Description 2");

        var initialCount = (await _toggleService.GetTogglesByServerId(serverId)).Count;

        // Act
        await _toggleService.CreateServerTogglesIfNotExistsAsync(serverId);

        // Assert
        var finalToggles = await _toggleService.GetTogglesByServerId(serverId);

        // Should have created default toggles but not duplicated existing ones
        Assert.True(finalToggles.Count >= initialCount);

        // Verify existing toggles weren't modified
        var existingToggle1 = finalToggles.FirstOrDefault(t => t.Name == "ExistingToggle1");
        Assert.NotNull(existingToggle1);
        Assert.True(existingToggle1.IsEnabled);
        Assert.Equal("Description 1", existingToggle1.Description);
    }
}