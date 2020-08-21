using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.MongoDb.Context
{
    internal class ContextTransaction : IEntityContextTransaction
    {
        public ContextTransaction()
        {
        }

        public void Commit()
        {
        }

        public void Dispose()
        {
        }

        public void Rollback()
        {
            throw new InvalidOperationException("Transactions aren't supported");
        }
    }
}