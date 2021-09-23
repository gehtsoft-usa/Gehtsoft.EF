using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.MongoDb
{
    public class MongoDeleteMultiEntityQuery : MongoQueryWithCondition
    {
        internal MongoDeleteMultiEntityQuery(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
        }

        public override async Task ExecuteAsync(CancellationToken? token = null)
        {
            await Collection.DeleteManyAsync(FilterBuilder.ToBsonDocument(), token ?? CancellationToken.None);
        }

        [ExcludeFromCodeCoverage]
        public override Task ExecuteAsync(object entity, CancellationToken? token = null)
        {
            throw new InvalidOperationException();
        }
    }
}
