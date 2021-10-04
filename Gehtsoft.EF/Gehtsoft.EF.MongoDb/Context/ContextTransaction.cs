using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.MongoDb.Context
{
    [ExcludeFromCodeCoverage]
    internal sealed class ContextTransaction : IEntityContextTransaction
    {
        public ContextTransaction()
        {
        }

        public void Commit()
        {
            // No transaction in MongoDB 
        }

        public void Dispose()
        {
            // Nothing to dispose for MongoDB
        }

        public void Rollback()
        {
            throw new InvalidOperationException("Transactions aren't supported");
        }
    }
}