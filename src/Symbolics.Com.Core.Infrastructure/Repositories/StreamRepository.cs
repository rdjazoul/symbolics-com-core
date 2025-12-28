using Microsoft.EntityFrameworkCore;
using Symbolics.Com.Core.Application.Repositories;
using Symbolics.Com.Core.Infrastructure.Persistence;

namespace Symbolics.Com.Core.Infrastructure.Repositories;

public sealed class StreamRepository(CoreDbContext dbContext) : IStreamRepository
{
    private readonly CoreDbContext _dbContext = dbContext;

    public Task<int> DeleteStreamsOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        return _dbContext.GamePlays
            .Where(entry => entry.Date < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
