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

        IEntityQuery IEntityContext.DropEntity(Type type)
        {
            return new ContextQuery(this.GetDropEntityQuery(type));
        }

        IEntityQuery IEntityContext.CreateEntity(Type type)
        {
            return new ContextQuery(this.GetCreateEntityQuery(type));
        }

        IModifyEntityQuery IEntityContext.InsertEntity(Type type, bool createKey)
        {
            return new ContextModifyQuery(this.GetInsertEntityQuery(type, !createKey));
        }

        IModifyEntityQuery IEntityContext.UpdateEntity(Type type)
        {
            return new ContextModifyQuery(this.GetUpdateEntityQuery(type));
        }

        IModifyEntityQuery IEntityContext.DeleteEntity(Type type)
        {
            return new ContextModifyQuery(this.GetDeleteEntityQuery(type));
        }

        IContextQueryWithCondition IEntityContext.DeleteMultiple(Type type)
        {
            return new ContextQueryWithCondition(this.GetMultiDeleteEntityQuery(type));
        }

        IContextSelect IEntityContext.Select(Type type)
        {
            return new ContextSelect(this.GetSelectEntitiesQuery(type));
        }
        IContextCount IEntityContext.Count(Type type)
        {
            return new ContextCount(this.GetSelectEntitiesCountQuery(type));
        }
    }
}