using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.MongoDb
{
    public class MongoDeleteListQuery : MongoQuery
    {
        internal MongoDeleteListQuery(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
        }

        public override async Task ExecuteAsync(CancellationToken? token = null)
        {
            if (CollectionExists)
                await Connection.Database.DropCollectionAsync(CollectionName, token ?? CancellationToken.None);
        }

        [ExcludeFromCodeCoverage]
        public override Task ExecuteAsync(object entity, CancellationToken? token = null)
        {
            throw new InvalidOperationException();
        }
    }
}
