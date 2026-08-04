using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories
{
    public class BaseRepository<T> : IBaseRepository<T> where T : BaseDbEntity
    {
        protected readonly WbDataBase Context;

        public BaseRepository(WbDataBase context)
        {
            Context = context;
        }

        public virtual async Task<T> GetAsync(int id)
        {
            return await Context.Set<T>().FirstOrDefaultAsync(x => x.Id == id);
        }

        public virtual async Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> selector)
        {
            return await Context.Set<T>().Where(selector).ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await Context.Set<T>().ToListAsync();
        }

        public virtual async Task<T> AddAsync(T newEntity)
        {
            var entity = await Context.Set<T>().AddAsync(newEntity);
            await Context.SaveChangesAsync();
            return entity.Entity;
        }

        public virtual async Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> newEntities)
        {
            await Context.Set<T>().AddRangeAsync(newEntities);
            await Context.SaveChangesAsync();
            return newEntities;
        }

        public virtual async Task RemoveAsync(int id)
        {
            var entity = await Context.Set<T>().FirstOrDefaultAsync(x => x.Id == id);
            if (entity != null) Context.Set<T>().Remove(entity);
            await Context.SaveChangesAsync();
        }

        public virtual async Task UpdateAsync(T entity)
        {
            DetachAllEntities();

            entity.UpdatedAt = DateTime.UtcNow;

            Context.Set<T>().Update(entity);
            await Context.SaveChangesAsync();
        }

        public virtual async Task UpdateRangeAsync(IEnumerable<T> entities)
        {
            var nowTime = DateTime.UtcNow;

            foreach (var item in entities)
            {
                item.UpdatedAt = nowTime;
            }

            Context.Set<T>().UpdateRange(entities);
            await Context.SaveChangesAsync();
        }

        public virtual async Task ReplaceAllAsync(IEnumerable<T> newEntities)
        {
            var set = Context.Set<T>().ToList();
            Context.RemoveRange(set);
            Context.AddRange(newEntities);
            await Context.SaveChangesAsync();
        }

        private void DetachAllEntities()
        {
            var undetachedEntriesCopy = Context.ChangeTracker.Entries()
                .Where(e => e.State != EntityState.Detached)
                .ToList();
            foreach (var entry in undetachedEntriesCopy)
                entry.State = EntityState.Detached;
        }
    }
}
