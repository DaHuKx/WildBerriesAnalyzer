using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories.Interfaces
{
    public interface IBaseRepository<T> where T : BaseDbEntity
    {
        Task<T> GetAsync(int id);

        Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> selector);

        Task<IEnumerable<T>> GetAllAsync();

        Task<T> AddAsync(T newEntity);

        Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> newEntities);

        Task RemoveAsync(int id);

        Task UpdateAsync(T entity);

        Task UpdateRangeAsync(IEnumerable<T> entities);

        Task ReplaceAllAsync(IEnumerable<T> newEntities);
    }
}
