using Amiquin.Core.Services.Chat.Toggle;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Services.Nacho;
using Amiquin.IntegrationTests.Fixtures;

namespace Amiquin.IntegrationTests.Services;

public class ServiceIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;
    private readonly IServerMetaService _serverMetaService;
    private readonly IToggleService _toggleService;
    private readonly INachoService _nachoService;

    public ServiceIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _serverMetaService = _fixture.ServiceProvider.GetRequiredService<IServerMetaService>();
        _toggleService = _fixture.ServiceProvider.GetRequiredService<IToggleService>();
        _nachoService = _fixture.ServiceProvider.GetRequiredService<INachoService>();
    }

    [Fact]
    public async Task CompleteServerWorkflow_ShouldWorkEndToEnd()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var serverId = 123456789UL;
        var serverName = "Integration Test Server";
        var userId1 = 111111111UL;
        var userId2 = 222222222UL;

        // Act & Assert - Step 1: Create server metadata
        var serverMeta = await _serverMetaService.CreateServerMetaAsync(serverId, serverName);
        Assert.NotNull(serverMeta);
        Assert.Equal(serverId, serverMeta.Id);
        Assert.Equal(serverName, serverMeta.ServerName);
        Assert.True(serverMeta.IsActive);

        // Act & Assert - Step 2: Set up toggles for the server
        await _toggleService.SetServerToggleAsync(serverId, "WelcomeMessages", true, "Enable welcome messages");
        await _toggleService.SetServerToggleAsync(serverId, "NachoRewards", true, "Enable nacho rewards");
        await _toggleService.SetServerToggleAsync(serverId, "ModeratorMode", false, "Disable moderator mode");

        var toggles = await _toggleService.GetTogglesByServerId(serverId);
        Assert.Equal(3, toggles.Count);
        Assert.True(await _toggleService.IsEnabledAsync(serverId, "WelcomeMessages"));
        Assert.True(await _toggleService.IsEnabledAsync(serverId, "NachoRewards"));
        Assert.False(await _toggleService.IsEnabledAsync(serverId, "ModeratorMode"));

        // Act & Assert - Step 3: Add nachos for users
        await _nachoService.AddNachoAsync(userId1, serverId, 3);
        await _nachoService.AddNachoAsync(userId2, serverId, 2);

        var user1NachoCount = await _nachoService.GetUserNachoCountAsync(userId1);
        var user2NachoCount = await _nachoService.GetUserNachoCountAsync(userId2);
        var serverNachoCount = await _nachoService.GetServerNachoCountAsync(serverId);
        var totalNachoCount = await _nachoService.GetTotalNachoCountAsync();

        Assert.Equal(3, user1NachoCount);
        Assert.Equal(2, user2NachoCount);
        Assert.Equal(5, serverNachoCount);
        Assert.Equal(5, totalNachoCount);

        // Act & Assert - Step 4: Update server metadata
        serverMeta.Persona = "A friendly and helpful bot";
        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

        var updatedServerMeta = await _serverMetaService.GetServerMetaAsync(serverId, true);
        Assert.NotNull(updatedServerMeta);
        Assert.Equal("A friendly and helpful bot", updatedServerMeta.Persona);
        Assert.Equal(3, updatedServerMeta.Toggles.Count);

        // Act & Assert - Step 5: Test toggle state changes affecting workflow
        await _toggleService.SetServerToggleAsync(serverId, "NachoRewards", false, "Temporarily disable nacho rewards");
        Assert.False(await _toggleService.IsEnabledAsync(serverId, "NachoRewards"));

        // Act & Assert - Step 6: Clean up specific user nachos
        await _nachoService.RemoveAllNachoAsync(userId1);

        var finalUser1Count = await _nachoService.GetUserNachoCountAsync(userId1);
        var finalUser2Count = await _nachoService.GetUserNachoCountAsync(userId2);
        var finalServerCount = await _nachoService.GetServerNachoCountAsync(serverId);

        Assert.Equal(0, finalUser1Count);
        Assert.Equal(2, finalUser2Count); // User2's nachos should remain
        Assert.Equal(2, finalServerCount); // Only User2's nachos remain
    }

    [Fact]
    public async Task MultiServerScenario_ShouldIsolateDataCorrectly()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var server1Id = 111111111UL;
        var server2Id = 222222222UL;
        var userId = 123456789UL;

        // Act - Set up two different servers
        await _serverMetaService.CreateServerMetaAsync(server1Id, "Server 1");
        await _serverMetaService.CreateServerMetaAsync(server2Id, "Server 2");

        // Configure different toggles for each server
        await _toggleService.SetServerToggleAsync(server1Id, "Feature1", true, "Enabled on server 1");
        await _toggleService.SetServerToggleAsync(server1Id, "Feature2", false, "Disabled on server 1");

        await _toggleService.SetServerToggleAsync(server2Id, "Feature1", false, "Disabled on server 2");
        await _toggleService.SetServerToggleAsync(server2Id, "Feature2", true, "Enabled on server 2");

        // Add nachos to each server
        await _nachoService.AddNachoAsync(userId, server1Id, 3);
        await _nachoService.AddNachoAsync(userId, server2Id, 2);

        // Assert - Verify data isolation
        var server1Toggles = await _toggleService.GetTogglesByServerId(server1Id);
        var server2Toggles = await _toggleService.GetTogglesByServerId(server2Id);

        Assert.Equal(2, server1Toggles.Count);
        Assert.Equal(2, server2Toggles.Count);

        // Verify toggle states are different between servers
        Assert.True(await _toggleService.IsEnabledAsync(server1Id, "Feature1"));
        Assert.False(await _toggleService.IsEnabledAsync(server1Id, "Feature2"));
        Assert.False(await _toggleService.IsEnabledAsync(server2Id, "Feature1"));
        Assert.True(await _toggleService.IsEnabledAsync(server2Id, "Feature2"));

        // Verify nacho counts are isolated by server
        var server1NachoCount = await _nachoService.GetServerNachoCountAsync(server1Id);
        var server2NachoCount = await _nachoService.GetServerNachoCountAsync(server2Id);
        var userTotalNachoCount = await _nachoService.GetUserNachoCountAsync(userId);

        Assert.Equal(3, server1NachoCount);
        Assert.Equal(2, server2NachoCount);
        Assert.Equal(5, userTotalNachoCount); // Total across all servers
    }

    [Fact]
    public async Task ServerDeactivation_ShouldAffectToggleBehavior()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var serverId = 123456789UL;

        // Create and configure server
        var serverMeta = await _serverMetaService.CreateServerMetaAsync(serverId, "Test Server");
        await _toggleService.SetServerToggleAsync(serverId, "TestToggle", true, "Test toggle");

        // Verify toggle works when server is active
        Assert.True(await _toggleService.IsEnabledAsync(serverId, "TestToggle"));

        // Act - Deactivate server
        serverMeta.IsActive = false;
        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

        // Assert - Toggle should return false for inactive server
        Assert.False(await _toggleService.IsEnabledAsync(serverId, "TestToggle"));

        // Act - Reactivate server
        serverMeta.IsActive = true;
        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

        // Assert - Toggle should work again
        Assert.True(await _toggleService.IsEnabledAsync(serverId, "TestToggle"));
    }

    [Fact]
    public async Task BulkOperations_ShouldMaintainDataConsistency()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var serverId = 123456789UL;

        await _serverMetaService.CreateServerMetaAsync(serverId, "Bulk Test Server");

        // Act - Bulk toggle operations
        var bulkToggles = new Dictionary<string, (bool IsEnabled, string? Description)>
        {
            { "Toggle1", (true, "Description 1") },
            { "Toggle2", (false, "Description 2") },
            { "Toggle3", (true, "Description 3") },
            { "Toggle4", (false, "Description 4") },
            { "Toggle5", (true, "Description 5") }
        };

        await _toggleService.SetServerTogglesBulkAsync(serverId, bulkToggles);

        // Assert - Verify all toggles were created correctly
        var toggles = await _toggleService.GetTogglesByServerId(serverId);
        Assert.Equal(5, toggles.Count);

        foreach (var expectedToggle in bulkToggles)
        {
            var actualToggle = toggles.FirstOrDefault(t => t.Name == expectedToggle.Key);
            Assert.NotNull(actualToggle);
            Assert.Equal(expectedToggle.Value.IsEnabled, actualToggle.IsEnabled);
            Assert.Equal(expectedToggle.Value.Description, actualToggle.Description);

            // Also verify through IsEnabledAsync
            var isEnabled = await _toggleService.IsEnabledAsync(serverId, expectedToggle.Key);
            Assert.Equal(expectedToggle.Value.IsEnabled, isEnabled);
        }

        // Act - Bulk nacho operations for multiple users
        var userIds = new[] { 111111111UL, 222222222UL, 333333333UL };
        foreach (var userId in userIds)
        {
            await _nachoService.AddNachoAsync(userId, serverId, 2);
        }

        // Assert - Verify nacho consistency
        var totalServerNachos = await _nachoService.GetServerNachoCountAsync(serverId);
        var totalNachos = await _nachoService.GetTotalNachoCountAsync();

        Assert.Equal(6, totalServerNachos); // 3 users × 2 nachos each
        Assert.Equal(6, totalNachos);

        foreach (var userId in userIds)
        {
            var userNachoCount = await _nachoService.GetUserNachoCountAsync(userId);
            Assert.Equal(2, userNachoCount);
        }
    }
}
