using Amiquin.Core.IRepositories;
using Amiquin.Core.Services.Meta;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Nacho;

/// <summary>
/// Service implementation for managing nacho operations.
/// Handles database operations for nacho counting, statistics, and management across users and servers.
/// </summary>
public class NachoService : INachoService
{
    public readonly INachoRepository _nachoRepository;
    private readonly IServerMetaService _serverMetaService;
    private readonly ILogger<NachoService> _logger;

    /// <summary>
    /// Initializes a new instance of the NachoService.
    /// </summary>
    /// <param name="nachoRepository">Repository for database operations on nacho records.</param>
    /// <param name="serverMetaService">Service for managing server metadata.</param>
    /// <param name="logger">Logger for service operations.</param>
    public NachoService(INachoRepository nachoRepository, IServerMetaService serverMetaService, ILogger<NachoService> logger)
    {
        _nachoRepository = nachoRepository;
        _serverMetaService = serverMetaService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> GetUserNachoCountAsync(ulong userId)
    {
        return await _nachoRepository.AsQueryable()
            .Where(x => x.UserId == userId)
            .SumAsync(x => x.NachoCount);
    }

    /// <inheritdoc/>
    public async Task<int> GetServerNachoCountAsync(ulong serverId)
    {
        return await _nachoRepository.AsQueryable()
            .Where(x => x.ServerId == serverId)
            .SumAsync(x => x.NachoCount);
    }

    /// <inheritdoc/>
    public async Task<int> GetTotalNachoCountAsync()
    {
        return await _nachoRepository.AsQueryable()
            .SumAsync(x => x.NachoCount);
    }

    /// <inheritdoc/>
    public async Task AddNachoAsync(ulong userId, ulong serverId, int nachoCount = 1)
    {
        if (nachoCount < 1)
        {
            throw new Exception("Hey, that's not cool. At least give 1 nacho.");
        }

        // Ensure server metadata exists before creating nacho records
        // This prevents foreign key constraint violations
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId);
        if (serverMeta == null)
        {
            _logger.LogWarning("ServerMeta not found for serverId {ServerId} when giving nacho, creating default metadata", serverId);

            // Create server metadata with a fallback name
            await _serverMetaService.CreateServerMetaAsync(serverId, $"Server_{serverId}");
        }

        var userTotalTodayNachos = await _nachoRepository.AsQueryable()
            .Where(x => x.UserId == userId && x.NachoReceivedDate.Date == DateTime.UtcNow.Date)
            .SumAsync(x => x.NachoCount);

        if (userTotalTodayNachos + nachoCount > 5)
        {
            throw new Exception("You can only give 5 nachos per day.");
        }

        var nacho = new Models.NachoPack
        {
            NachoCount = nachoCount,
            UserId = userId,
            ServerId = serverId,
            NachoReceivedDate = DateTime.UtcNow
        };

        await _nachoRepository.AddAsync(nacho);
        await _nachoRepository.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task RemoveNachoAsync(ulong userId, ulong serverId)
    {
        var nacho = await _nachoRepository.AsQueryable()
            .Where(x => x.UserId == userId && x.ServerId == serverId)
            .FirstOrDefaultAsync();

        if (nacho is not null)
        {
            await _nachoRepository.RemoveAsync(nacho);
            await _nachoRepository.SaveChangesAsync();
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAllNachoAsync(ulong userId)
    {
        var nachos = await _nachoRepository.AsQueryable()
            .Where(x => x.UserId == userId)
            .ToListAsync();

        if (nachos.Any())
        {
            await _nachoRepository.RemoveRangeAsync(nachos);
            await _nachoRepository.SaveChangesAsync();
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAllServerNachoAsync(ulong serverId)
    {
        var nachos = await _nachoRepository.AsQueryable()
            .Where(x => x.ServerId == serverId)
            .ToListAsync();

        if (nachos.Count != 0)
        {
            await _nachoRepository.RemoveRangeAsync(nachos);
            await _nachoRepository.SaveChangesAsync();
        }
    }
}