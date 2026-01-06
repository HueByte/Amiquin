using Amiquin.Core.DiscordExtensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Amiquin.Core.Services.BotContext;

/// <summary>
/// Thread-safe accessor for bot context information during Discord interactions.
/// Provides centralized access to context data, server metadata, and execution tracking.
/// </summary>
public class BotContextAccessor : IDisposable
{
    private readonly ConcurrentDictionary<string, object> _contextData = new();
    private readonly ILogger<BotContextAccessor>? _logger;
    private readonly object _lock = new();
    private readonly Timer _timeoutTimer;
    private volatile bool _disposed;
    private volatile bool _isFinished;

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5); // 5 minute timeout

    /// <summary>
    /// Gets the bot name from configuration or uses default value.
    /// </summary>
    public string BotName { get; private set; } = "Amiquin";

    /// <summary>
    /// Gets the bot version from configuration or uses default value.
    /// </summary>
    public string BotVersion { get; private set; } = "Unknown";

    /// <summary>
    /// Gets the unique identifier for this context instance.
    /// </summary>
    public string ContextId { get; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Gets the server ID from the current Discord context.
    /// </summary>
    public ulong ServerId => Context?.Guild?.Id ?? 0;

    /// <summary>
    /// Gets the user ID from the current Discord context.
    /// </summary>
    public ulong UserId => Context?.User?.Id ?? 0;

    /// <summary>
    /// Gets the channel ID from the current Discord context.
    /// </summary>
    public ulong ChannelId => Context?.Channel?.Id ?? 0;

    /// <summary>
    /// Gets the server metadata associated with this context.
    /// </summary>
    public Models.ServerMeta? ServerMeta { get; private set; }

    /// <summary>
    /// Gets the Discord interaction context.
    /// </summary>
    public ExtendedShardedInteractionContext? Context { get; private set; }

    /// <summary>
    /// Gets the timestamp when this context was created.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Gets the timestamp when the context execution finished.
    /// </summary>
    public DateTime FinishedAt { get; private set; }

    /// <summary>
    /// Gets the execution duration in milliseconds.
    /// </summary>
    public long ExecutionTimeMs => _isFinished
        ? (long)(FinishedAt - CreatedAt).TotalMilliseconds
        : (long)(DateTime.UtcNow - CreatedAt).TotalMilliseconds;

    /// <summary>
    /// Gets whether the context execution has finished.
    /// </summary>
    public bool IsFinished => _isFinished;

    /// <summary>
    /// Gets whether the context has been initialized with Discord context and server metadata.
    /// </summary>
    public bool IsInitialized => Context is not null && ServerMeta is not null;

    /// <summary>
    /// Initializes a new instance of the BotContextAccessor.
    /// </summary>
    /// <param name="logger">Optional logger for context operations.</param>
    public BotContextAccessor(ILogger<BotContextAccessor>? logger = null)
    {
        CreatedAt = DateTime.UtcNow;
        _logger = logger;

        // Set up timeout timer to automatically finish context after timeout period
        _timeoutTimer = new Timer(OnTimeout, null, DefaultTimeout, Timeout.InfiniteTimeSpan);

        _logger?.LogTrace("Created new BotContextAccessor with ID: {ContextId} with {Timeout}ms timeout",
            ContextId, DefaultTimeout.TotalMilliseconds);
    }

    /// <summary>
    /// Initializes the context with Discord interaction data, server metadata, and configuration.
    /// </summary>
    /// <param name="context">The Discord interaction context.</param>
    /// <param name="serverMeta">The server metadata.</param>
    /// <param name="config">The application configuration.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or serverMeta is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when context is already initialized or disposed.</exception>
    public void Initialize(ExtendedShardedInteractionContext context, Models.ServerMeta serverMeta, IConfiguration config)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(serverMeta);
        ArgumentNullException.ThrowIfNull(config);

        lock (_lock)
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException("Context is already initialized");
            }

            Context = context;
            ServerMeta = serverMeta;

            // Load configuration values with fallback to defaults
            BotName = config.GetValue<string>("Bot:Name") ?? "Amiquin";
            BotVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

            _logger?.LogDebug("Initialized BotContextAccessor {ContextId} for server {ServerId} ({ServerName})",
                ContextId, ServerId, serverMeta.ServerName);
        }
    }

    /// <summary>
    /// Marks the context execution as finished and records the finish timestamp.
    /// </summary>
    public void Finish()
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);

        lock (_lock)
        {
            if (_isFinished)
            {
                _logger?.LogDebug("Context {ContextId} finish called multiple times - ignoring duplicate call", ContextId);
                return;
            }

            FinishedAt = DateTime.UtcNow;
            _isFinished = true;

            // Stop the timeout timer since we've finished normally
            try
            {
                _timeoutTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
            catch (ObjectDisposedException)
            {
                // If the timer is already disposed, we can safely ignore.
            }

            _logger?.LogDebug("Finished BotContextAccessor {ContextId} execution in {ExecutionTime}ms",
                ContextId, ExecutionTimeMs);
        }
    }

    /// <summary>
    /// Timeout callback that automatically finishes the context if it hasn't been finished manually.
    /// </summary>
    private void OnTimeout(object? state)
    {
        if (_disposed || _isFinished) return;

        lock (_lock)
        {
            if (_disposed || _isFinished) return;

            FinishedAt = DateTime.UtcNow;
            _isFinished = true;

            _logger?.LogWarning("BotContextAccessor {ContextId} timed out after {Timeout}ms and was automatically finished",
                ContextId, DefaultTimeout.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Updates the server metadata for this context.
    /// </summary>
    /// <param name="serverMeta">The new server metadata.</param>
    /// <exception cref="ArgumentNullException">Thrown when serverMeta is null.</exception>
    public void SetServerMeta(Models.ServerMeta serverMeta)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
        ArgumentNullException.ThrowIfNull(serverMeta);

        lock (_lock)
        {
            ServerMeta = serverMeta;
            _logger?.LogTrace("Updated ServerMeta for context {ContextId}", ContextId);
        }
    }

    /// <summary>
    /// Updates the Discord interaction context.
    /// </summary>
    /// <param name="context">The new Discord interaction context.</param>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    public void SetContext(ExtendedShardedInteractionContext context)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
        ArgumentNullException.ThrowIfNull(context);

        lock (_lock)
        {
            Context = context;
            _logger?.LogTrace("Updated Context for context {ContextId}", ContextId);
        }
    }

    /// <summary>
    /// Stores arbitrary data associated with this context.
    /// </summary>
    /// <param name="key">The key to store the data under.</param>
    /// <param name="value">The value to store.</param>
    public void SetContextData(string key, object value)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _contextData.AddOrUpdate(key, value, (_, _) => value);

        _logger?.LogTrace("Set context data {Key} for context {ContextId}", key, ContextId);
    }

    /// <summary>
    /// Retrieves data associated with this context.
    /// </summary>
    /// <typeparam name="T">The type of data to retrieve.</typeparam>
    /// <param name="key">The key of the data to retrieve.</param>
    /// <returns>The stored data or default value if not found.</returns>
    public T? GetContextData<T>(string key)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_contextData.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }

        return default;
    }

    /// <summary>
    /// Gets a summary of the context information for logging or debugging.
    /// </summary>
    /// <returns>A formatted string containing context information.</returns>
    public string GetContextSummary()
    {
        var summary = $"Context {ContextId}: ";

        if (IsInitialized)
        {
            summary += $"Server={ServerMeta?.ServerName} ({ServerId}), " +
                      $"User={Context?.User?.Username} ({UserId}), " +
                      $"Channel={ChannelId}, " +
                      $"Duration={ExecutionTimeMs}ms, " +
                      $"Finished={IsFinished}";
        }
        else
        {
            summary += "Not initialized";
        }

        return summary;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;

            if (!_isFinished)
            {
                Finish();
            }

            // Clean up context data
            _contextData.Clear();

            // Dispose timer after finishing (Finish stops the timer)
            _timeoutTimer.Dispose();

            _disposed = true;
            _logger?.LogTrace("Disposed BotContextAccessor {ContextId} after {ExecutionTime}ms",
                ContextId, ExecutionTimeMs);
        }

        GC.SuppressFinalize(this);
    }

}