using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class Repository<T>(NetZeroGatewayDbContext db) : IRepository<T>
    where T : BaseEntity
{
    protected readonly DbSet<T>                    _set = db.Set<T>();
    protected readonly NetZeroGatewayDbContext      _db  = db;

    public Task<T?>      GetByIdAsync(Guid id)
        => _set.FirstOrDefaultAsync(e => e.Id == id);
    public Task<List<T>> GetAllAsync()
        => _set.ToListAsync();
    public Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate)
        => _set.Where(predicate).ToListAsync();
    public async Task    AddAsync(T entity)
        => await _set.AddAsync(entity);
    public void          Update(T entity)
        => _set.Update(entity);
    public void          Delete(T entity)
        => _set.Remove(entity);
    public Task<int>     SaveChangesAsync()
        => _db.SaveChangesAsync();
}
