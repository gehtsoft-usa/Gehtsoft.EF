using System;

namespace Gehtsoft.EF.MongoDb
{
    internal sealed class MongoConditionalQueryWhereGroup : IDisposable
    {
        private IMongoConditionalQueryWhereTarget mQuery;
        internal MongoConditionalQueryWhereGroup(IMongoConditionalQueryWhereTarget query)
        {
            mQuery = query;
        }

        public void Dispose()
        {
            mQuery?.EndWhereGroup(this);
            mQuery = null;
        }
    }
}
