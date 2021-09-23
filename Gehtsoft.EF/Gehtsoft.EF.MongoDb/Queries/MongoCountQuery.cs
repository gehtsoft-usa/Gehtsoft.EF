using System;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics.CodeAnalysis;

namespace Gehtsoft.EF.MongoDb
{
    public class MongoCountQuery : MongoQueryWithCondition
    {
        private long? mRowCount = null;

        public long RowCount
        {
            get
            {
                if (mRowCount == null)
                    Execute();
                return mRowCount ?? 0;
            }
        }

        internal MongoCountQuery(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
        }

        public override async Task ExecuteAsync(CancellationToken? token = null)
        {
            mRowCount = await Collection.CountDocumentsAsync(FilterBuilder.ToBsonDocument(), null, token ?? CancellationToken.None);
        }

        [ExcludeFromCodeCoverage]
        public override Task ExecuteAsync(object entity, CancellationToken? token = null)
        {
            throw new InvalidOperationException();
        }
    }
}
