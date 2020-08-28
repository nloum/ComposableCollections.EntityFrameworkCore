using System;
using System.Linq.Expressions;
using ComposableCollections.Dictionary.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ComposableCollections.EntityFrameworkCore
{
    public static class Extensions
    {
        public static IQueryableDictionary<TId, TDbDto> AsQueryableDictionary<TId, TDbDto, TDbContext>(this TDbContext dbContext, Func<TDbContext, DbSet<TDbDto>> dbSet, Expression<Func<TDbDto, TId>> id) where TDbContext : DbContext where TDbDto : class
        {
            var theDbSet = dbSet(dbContext);
            var efCoreDict = new EntityFrameworkCoreDictionary<TId, TDbDto, TDbContext>(dbContext, theDbSet, id);
            return efCoreDict;
        }

        public static IQueryableReadOnlyDictionary<TId, TDbDto> AsQueryableReadOnlyDictionary<TId, TDbDto, TDbContext>(
            this TDbContext dbContext, Func<TDbContext, DbSet<TDbDto>> dbSet, Expression<Func<TDbDto, TId>> id) where TDbContext : DbContext where TDbDto : class
        {
            return dbSet(dbContext).AsQueryableReadOnlyDictionary(id);
        }
    }
}