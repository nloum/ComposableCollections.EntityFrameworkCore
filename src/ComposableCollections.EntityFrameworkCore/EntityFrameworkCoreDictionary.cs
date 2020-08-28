using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using AutoMapper;
using ComposableCollections.Dictionary;
using ComposableCollections.Dictionary.Base;
using ComposableCollections.Dictionary.Interfaces;
using ComposableCollections.Dictionary.Write;
using Microsoft.EntityFrameworkCore;
using SimpleMonads;

namespace ComposableCollections.EntityFrameworkCore
{
    public class EntityFrameworkCoreDictionary<TId, TDbDto, TDbContext> : DictionaryBase<TId, TDbDto>, IQueryableDictionary<TId, TDbDto> where TDbDto : class where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;
        private readonly DbSet<TDbDto> _dbSet;
        private readonly IMapper _mapper;
        private readonly Expression<Func<TDbDto, TId>> _getKey;
        private readonly Func<TId, Expression<Func<TDbDto, bool>>> _compareKey;
        private readonly Expression<Func<TDbDto, IKeyValue<TId, TDbDto>>> _getKeyValue;

        public EntityFrameworkCoreDictionary(TDbContext dbContext, DbSet<TDbDto> dbSet, Expression<Func<TDbDto, TId>> getKey)
        {
            _dbContext = dbContext;
            _dbSet = dbSet;
            _getKey = getKey;
            
            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<TDbDto, TDbDto>();
            });

            _mapper = mapperConfig.CreateMapper();
            
            var memberExpression = getKey.Body as MemberExpression;
            _compareKey = key =>
            {
                var parameter = Expression.Parameter(typeof(TDbDto), "p1");
                var equality = Expression.Equal(Expression.MakeMemberAccess(parameter, memberExpression.Member), Expression.Constant(key, typeof(TId)));
                var result = Expression.Lambda<Func<TDbDto, bool>>(equality, parameter);
                return result;
            };

            var valueParameter = Expression.Parameter(typeof(TDbDto), "p1");
            var body = Expression.New(typeof(KeyValue<TId, TDbDto>).GetConstructor(new[] {typeof(TId), typeof(TDbDto)}),
                Expression.MakeMemberAccess(valueParameter, memberExpression.Member),
                valueParameter);
            _getKeyValue = Expression.Lambda<Func<TDbDto, IKeyValue<TId, TDbDto>>>(body, valueParameter);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override IEnumerator<IKeyValue<TId, TDbDto>> GetEnumerator()
        {
            return _dbSet.Select(_getKeyValue).AsEnumerable().GetEnumerator();
        }

        public override IEnumerable<TDbDto> Values => _dbSet;
        IQueryable<TDbDto> IQueryableReadOnlyDictionary<TId, TDbDto>.Values => _dbSet;

        public override int Count => _dbSet.Count();
        public override IEqualityComparer<TId> Comparer => EqualityComparer<TId>.Default;

        public override IEnumerable<TId> Keys => _dbSet.Select(_getKey);

        public override bool ContainsKey(TId key)
        {
            return _dbSet.Where(_compareKey(key)).FirstOrDefault() != null;
        }

        public override bool TryGetValue(TId key, out TDbDto value)
        {
            value = _dbSet.Where(_compareKey(key)).FirstOrDefault();
            return value != null;
        }

        public override void Write(IEnumerable<DictionaryWrite<TId, TDbDto>> mutations, out IReadOnlyList<DictionaryWriteResult<TId, TDbDto>> results)
        {
            var finalResults = new List<DictionaryWriteResult<TId, TDbDto>>();
            results = finalResults;

            using (var transaction = _dbContext.Database.BeginTransaction())
            {
                try
                {
                    foreach (var mutation in mutations)
                    {
                        if (mutation.Type == DictionaryWriteType.Add)
                        {
                            if (TryGetValue(mutation.Key, out var preExistingValue))
                            {
                                _dbContext.Entry(preExistingValue).State = EntityState.Detached;
                                throw new InvalidOperationException("Cannot add an item when an item with that key already exists");
                            }

                            var newValue = mutation.ValueIfAdding.Value();
                            _dbSet.Add(newValue);
                            finalResults.Add(DictionaryWriteResult<TId, TDbDto>.CreateAdd(mutation.Key, true, Maybe<TDbDto>.Nothing(), newValue.ToMaybe()));
                        }
                        else if (mutation.Type == DictionaryWriteType.TryAdd)
                        {
                            if (TryGetValue(mutation.Key, out var preExistingValue))
                            {
                                _dbContext.Entry(preExistingValue).State = EntityState.Detached;
                                finalResults.Add(DictionaryWriteResult<TId, TDbDto>.CreateAdd(mutation.Key, false, preExistingValue.ToMaybe(), Maybe<TDbDto>.Nothing()));
                            }
                            else
                            {
                                var newValue = mutation.ValueIfAdding.Value();
                                _dbSet.Add(newValue);
                                finalResults.Add(DictionaryWriteResult<TId, TDbDto>.CreateAdd(mutation.Key, true, Maybe<TDbDto>.Nothing(), newValue.ToMaybe()));
                            }
                        }
                        else if (mutation.Type == DictionaryWriteType.Update)
                        {
                            if (TryGetValue(mutation.Key, out var preExistingValue))
                            {
                                var oldPreExistingValue = _mapper.Map<TDbDto, TDbDto>(preExistingValue);

                                var updatedValue = mutation.ValueIfUpdating.Value(preExistingValue);
                                _mapper.Map(updatedValue, preExistingValue);
                                _dbSet.Update(preExistingValue);
                                finalResults.Add(DictionaryWriteResult<TId, TDbDto>.CreateUpdate(mutation.Key, true, oldPreExistingValue.ToMaybe(), preExistingValue.ToMaybe()));
                            }
                            else
                            {
                                throw new InvalidOperationException("Cannot update an item when no item with that key already exists");
                            }
                        }
                        else if (mutation.Type == DictionaryWriteType.TryUpdate)
                        {
                            if (TryGetValue(mutation.Key, out var preExistingValue))
                            {
                                var oldPreExistingValue = _mapper.Map<TDbDto, TDbDto>(preExistingValue);
                                var updatedValue = mutation.ValueIfUpdating.Value(preExistingValue);
                                _mapper.Map(updatedValue, preExistingValue);
                                _dbSet.Update(preExistingValue);
                                finalResults.Add(DictionaryWriteResult<TId, TDbDto>.CreateUpdate(mutation.Key, true, oldPreExistingValue.ToMaybe(), preExistingValue.ToMaybe()));
                            }
                            else
                            {
                                finalResults.Add(DictionaryWriteResult<TId, TDbDto>.CreateUpdate(mutation.Key, true, Maybe<TDbDto>.Nothing(), Maybe<TDbDto>.Nothing()));
                            }
                        }
                        else if (mutation.Type == DictionaryWriteType.Remove)
                        {
                            if (TryGetValue(mutation.Key, out var preExistingValue))
                            {
                                _dbContext.Entry(preExistingValue).State = EntityState.Detached;
                                _dbSet.Remove(preExistingValue);
                                finalResults.Add(DictionaryWriteResult<TId, TDbDto>.CreateRemove(mutation.Key, preExistingValue.ToMaybe()));
                            }
                            else
                            {
                                throw new InvalidOperationException("Cannot remove an item when no item with that key already exists");
                            }
                        }
                        else if (mutation.Type == DictionaryWriteType.TryRemove)
                        {
                            if (TryGetValue(mutation.Key, out var preExistingValue))
                            {
                                _dbContext.Entry(preExistingValue).State = EntityState.Detached;
                                _dbSet.Remove(preExistingValue);
                                finalResults.Add(DictionaryWriteResult<TId, TDbDto>.CreateRemove(mutation.Key, preExistingValue.ToMaybe()));
                            }
                            else
                            {
                                finalResults.Add(DictionaryWriteResult<TId, TDbDto>.CreateRemove(mutation.Key, Maybe<TDbDto>.Nothing()));
                            }
                        }
                        else if (mutation.Type == DictionaryWriteType.AddOrUpdate)
                        {
                            if (TryGetValue(mutation.Key, out var preExistingValue))
                            {
                                var oldPreExistingValue = _mapper.Map<TDbDto, TDbDto>(preExistingValue);
                                var updatedValue = mutation.ValueIfUpdating.Value(preExistingValue);
                                _mapper.Map(updatedValue, preExistingValue);
                                _dbSet.Update(preExistingValue);
                                finalResults.Add(DictionaryWriteResult<TId, TDbDto>.CreateAddOrUpdate(mutation.Key, DictionaryItemAddOrUpdateResult.Update, oldPreExistingValue.ToMaybe(), preExistingValue));
                            }
                            else
                            {
                                var updatedValue = mutation.ValueIfAdding.Value();
                                _dbSet.Add(updatedValue);
                                finalResults.Add(DictionaryWriteResult<TId, TDbDto>.CreateAddOrUpdate(mutation.Key, DictionaryItemAddOrUpdateResult.Update, Maybe<TDbDto>.Nothing(), updatedValue));
                            }
                        }
                    }

                    _dbContext.SaveChanges();
                    transaction.Commit();
                }
                catch (Exception e)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }
}