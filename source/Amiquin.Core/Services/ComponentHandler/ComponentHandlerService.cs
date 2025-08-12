using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Amiquin.Core.Services.ComponentHandler;

/// <summary>
/// Implementation of the component handler service with registry-based routing.
/// </summary>
public class ComponentHandlerService : IComponentHandlerService
{
    private readonly ILogger<ComponentHandlerService> _logger;
    private readonly ConcurrentDictionary<string, Func<SocketMessageComponent, ComponentContext, Task<bool>>> _handlers = new();
    private readonly HashSet<string> _modalTriggers = new();

    /// <summary>
    /// The separator used in custom IDs to separate prefix and parameters.
    /// </summary>
    public const string CustomIdSeparator = ":";

    public ComponentHandlerService(ILogger<ComponentHandlerService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> HandleInteractionAsync(SocketMessageComponent component)
    {
        var context = ParseCustomId(component.Data.CustomId);
        if (context == null)
        {
            _logger.LogDebug("Could not parse custom ID: {CustomId}", component.Data.CustomId);
            return false;
        }

        if (!_handlers.TryGetValue(context.Prefix, out var handler))
        {
            _logger.LogDebug("No handler registered for prefix: {Prefix}", context.Prefix);
            return false;
        }

        try
        {
            _logger.LogDebug("Handling component interaction with prefix {Prefix} for user {UserId}",
                context.Prefix, component.User.Id);

            var handled = await handler(component, context);

            if (handled)
            {
                _logger.LogDebug("Component interaction {CustomId} handled successfully", component.Data.CustomId);
            }

            return handled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling component interaction {CustomId} with prefix {Prefix}",
                component.Data.CustomId, context.Prefix);
            return false;
        }
    }

    /// <inheritdoc/>
    public void RegisterHandler(string prefix, Func<SocketMessageComponent, ComponentContext, Task<bool>> handler)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix cannot be null or whitespace", nameof(prefix));

        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        if (!_handlers.TryAdd(prefix, handler))
        {
            // Update the existing handler instead of warning - this allows services to re-register
            _handlers[prefix] = handler;
            _logger.LogDebug("Updated component handler for prefix: {Prefix}", prefix);
        }
        else
        {
            _logger.LogDebug("Registered component handler for prefix: {Prefix}", prefix);
        }
    }

    /// <inheritdoc/>
    public string GenerateCustomId(string prefix, params string[] parameters)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix cannot be null or whitespace", nameof(prefix));

        // Validate that parameters don't contain the separator
        if (parameters.Any(p => p?.Contains(CustomIdSeparator) == true))
            throw new ArgumentException($"Parameters cannot contain the separator '{CustomIdSeparator}'");

        var parts = new List<string> { prefix };
        parts.AddRange(parameters ?? Array.Empty<string>());

        var customId = string.Join(CustomIdSeparator, parts);

        // Discord custom IDs have a 100 character limit
        if (customId.Length > 100)
        {
            _logger.LogWarning("Generated custom ID exceeds Discord's 100 character limit: {Length} chars", customId.Length);
        }

        return customId;
    }

    /// <inheritdoc/>
    public bool WillTriggerModal(string customId)
    {
        var context = ParseCustomId(customId);
        if (context == null) return false;

        // Check if the entire prefix is registered as a modal trigger
        if (_modalTriggers.Contains(context.Prefix)) return true;

        // Check for specific parameter-based modal triggers
        // Format: "prefix:param1" where param1 determines if it's a modal
        if (context.Parameters.Length > 0)
        {
            var specificTrigger = $"{context.Prefix}:{context.Parameters[0]}";
            return _modalTriggers.Contains(specificTrigger);
        }

        return false;
    }

    /// <inheritdoc/>
    public void RegisterModalTrigger(string prefix)
    {
        _modalTriggers.Add(prefix);
        _logger.LogDebug("Registered modal trigger for prefix: {Prefix}", prefix);
    }

    /// <inheritdoc/>
    public ComponentContext? ParseCustomId(string customId)
    {
        if (string.IsNullOrWhiteSpace(customId))
            return null;

        var parts = customId.Split(CustomIdSeparator, StringSplitOptions.None);
        if (parts.Length < 1)
            return null;

        var prefix = parts[0];
        var parameters = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

        return new ComponentContext(prefix, parameters, customId);
    }

    /// <summary>
    /// Gets all registered prefixes for debugging purposes.
    /// </summary>
    /// <returns>An enumerable of all registered prefixes.</returns>
    public IEnumerable<string> GetRegisteredPrefixes()
    {
        return _handlers.Keys;
    }
}