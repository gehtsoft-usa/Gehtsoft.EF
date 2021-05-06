using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
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

        public override async Task ExecuteAsync()
        {
            if (CollectionExists)
                await Connection.Database.DropCollectionAsync(CollectionName);
        }
        public override async Task ExecuteAsync(CancellationToken token)
        {
            if (CollectionExists)
                await Connection.Database.DropCollectionAsync(CollectionName, token);
        }

        public override Task ExecuteAsync(object entity)
        {
            throw new InvalidOperationException();
        }
        public override Task ExecuteAsync(object entity, CancellationToken token)
        {
            throw new InvalidOperationException();
        }
    }
}
