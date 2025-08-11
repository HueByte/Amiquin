using Discord;
using Discord.WebSocket;

namespace Amiquin.Core.Services.Configuration;

/// <summary>
/// Service for handling server configuration interactions with Discord Components V2.
/// </summary>
public interface IConfigurationInteractionService
{
    /// <summary>
    /// Creates a configuration interface using Components V2.
    /// </summary>
    Task<(Embed embed, MessageComponent components)> CreateConfigurationInterfaceAsync(ulong guildId, SocketGuild guild);
    
    /// <summary>
    /// Initializes and registers all configuration-related component handlers.
    /// </summary>
    void Initialize();
}