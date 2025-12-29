using Microsoft.EntityFrameworkCore;
using Symbolics.Com.Core.Application.Repositories;
using Symbolics.Com.Core.Infrastructure.Persistence;

namespace Symbolics.Com.Core.Infrastructure.Repositories;

public sealed class SystemSettingsRepository(CoreDbContext dbContext) : ISystemSettingsRepository
{
    private readonly CoreDbContext _dbContext = dbContext;

    public async Task<int> GetIntSettingAsync(string key, int defaultValue, CancellationToken cancellationToken = default)
    {
        var value = await _dbContext.SystemSettings
            .AsNoTracking()
            .Where(setting => setting.Key == key)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}
