using System;
using ComposableCollections.Common;
using Microsoft.EntityFrameworkCore;

namespace ComposableCollections.EntityFrameworkCore
{
    public class DbContextFactory<TDbContext> : IReadWriteFactory<TDbContext, TDbContext> where TDbContext : DbContext
    {
        private Func<TDbContext> _create;
        
        public DbContextFactory(Func<TDbContext> create,
            Action<TDbContext> migrate = null)
        {
            if (migrate != null)
            {
                var hasMigratedYet = false;
                var simpleCreate = create;
                create = () =>
                {
                    if (!hasMigratedYet)
                    {
                        using (var context = simpleCreate())
                        {
                            migrate(context);
                        }
                    }

                    return simpleCreate();
                };
            }

            _create = create;
        }

        public TDbContext CreateReader()
        {
            return _create();
        }

        public TDbContext CreateWriter()
        {
            return _create();
        }
    }
}