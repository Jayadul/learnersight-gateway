using System.Linq.Expressions;
using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?>      GetByIdAsync(Guid id);
    Task<List<T>> GetAllAsync();
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task          AddAsync(T entity);
    void          Update(T entity);
    void          Delete(T entity);
    Task<int>     SaveChangesAsync();
}
