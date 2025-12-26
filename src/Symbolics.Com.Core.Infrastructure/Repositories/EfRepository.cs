using Microsoft.EntityFrameworkCore;
using Symbolics.Com.Core.Application.Repositories;
using Symbolics.Com.Core.Infrastructure.Persistence;

namespace Symbolics.Com.Core.Infrastructure.Repositories;

public sealed class EfRepository<T>(CoreDbContext dbContext) : IRepository<T> where T : class
{
    private readonly CoreDbContext _dbContext = dbContext;

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<T>().Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<T>().FindAsync([id], cancellationToken).AsTask();
    }

    public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbContext.Entry(entity).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
