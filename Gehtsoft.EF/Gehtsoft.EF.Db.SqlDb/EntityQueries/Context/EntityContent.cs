using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Context;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb
{
    public partial class SqlDbConnection : IEntityContext
    {
        IEntityContextTransaction IEntityContext.BeginTransaction()
        {
            return new ContextTransaction(BeginTransaction());
        }

        public IEntityQuery DropEntity(Type type)
        {
            return new ContextQuery(this.GetDropEntityQuery(type));
        }

        public IEntityQuery CreateEntity(Type type)
        {
            return new ContextQuery(this.GetCreateEntityQuery(type));
        }

        public IModifyEntityQuery InsertEntity(Type type, bool createKey)
        {
            return new ContextModifyQuery(this.GetInsertEntityQuery(type, !createKey));
        }

        public IModifyEntityQuery UpdateEntity(Type type)
        {
            return new ContextModifyQuery(this.GetUpdateEntityQuery(type));
        }

        public IModifyEntityQuery DeleteEntity(Type type)
        {
            return new ContextModifyQuery(this.GetDeleteEntityQuery(type));
        }

        public IContextQueryWithCondition DeleteMultiple(Type type)
        {
            return new ContextQueryWithCondition(this.GetMultiDeleteEntityQuery(type));
        }

        public IContextSelect Select(Type type)
        {
            return new ContextSelect(this.GetSelectEntitiesQuery(type));
        }

        public IContextCount Count(Type type)
        {
            return new ContextCount(this.GetSelectEntitiesCountQuery(type));
        }
    }
}