using System;
using System.Linq.Expressions;
using ComposableCollections.Common;
using ComposableCollections.Dictionary.Adapters;
using ComposableCollections.Dictionary.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using UtilityDisposables;

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

        public static
            IReadWriteFactory<IDisposableQueryableReadOnlyDictionary<TId, TDbDto>, IDisposableQueryableDictionary<TId, TDbDto>>
            WithDatabaseTable<TDbContext, TId, TDbDto>(this IReadWriteFactory<TDbContext, TDbContext> source,
                Func<TDbContext, DbSet<TDbDto>> dbSet, Expression<Func<TDbDto, TId>> id)
        where TDbDto : class
        where TDbContext : DbContext
        {
            return new AnonymousReadWriteFactory<IDisposableQueryableReadOnlyDictionary<TId, TDbDto>, IDisposableQueryableDictionary<TId, TDbDto>>(
                () =>
                {
                    var dbContext = source.CreateReader();
                    return new DisposableQueryableReadOnlyDictionaryAdapter<TId, TDbDto>(
                        dbContext.AsQueryableReadOnlyDictionary(dbSet, id),
                        dbContext);
                }, () =>
                {
                    var dbContext = source.CreateWriter();
                    return new DisposableQueryableDictionaryAdapter<TId, TDbDto>(
                        dbContext.AsQueryableDictionary(dbSet, id),
                        new AnonymousDisposable(() =>
                        {
                            dbContext.SaveChanges();
                            dbContext.Dispose();
                        }));
                });
        }
    }
}