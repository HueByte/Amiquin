using Amiquin.Core.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Amiquin.Core.Services.Nacho;

public class NachoService : INachoService
{
    public readonly INachoRepository _nachoRepository;

    public NachoService(INachoRepository nachoRepository)
    {
        _nachoRepository = nachoRepository;
    }

    public async Task<int> GetUserNachoCountAsync(ulong userId)
    {
        return await _nachoRepository.AsQueryable()
            .Where(x => x.UserId == userId)
            .SumAsync(x => x.NachoCount);
    }

    public async Task<int> GetServerNachoCountAsync(ulong serverId)
    {
        return await _nachoRepository.AsQueryable()
            .Where(x => x.ServerId == serverId)
            .SumAsync(x => x.NachoCount);
    }

    public async Task<int> GetTotalNachoCountAsync()
    {
        return await _nachoRepository.AsQueryable()
            .SumAsync(x => x.NachoCount);
    }

    public async Task AddNachoAsync(ulong userId, ulong serverId, int nachoCount = 1)
    {
        if (nachoCount < 1)
        {
            throw new Exception("Hey, that's not cool. At least give 1 nacho.");
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