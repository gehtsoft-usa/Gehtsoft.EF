using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Context;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using Gehtsoft.EF.MongoDb.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.MongoDb
{
    public sealed partial class MongoConnection : IEntityContext
    {
        IEntityContextTransaction IEntityContext.BeginTransaction()
        {
            return new ContextTransaction();
        }

        IEntityQuery IEntityContext.DropEntity(Type type)
        {
            return new ContextQuery(this.GetDeleteListQuery(type));
        }

        IEntityQuery IEntityContext.CreateEntity(Type type)
        {
            return new ContextQuery(this.GetCreateListQuery(type));
        }

        IModifyEntityQuery IEntityContext.InsertEntity(Type type, bool createKey)
        {
            return new ContextModifyQuery(this.GetInsertEntityQuery(type));
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
            return new ContextQueryWithCondition(this.GetDeleteMultiEntityQuery(type));
        }

        IContextSelect IEntityContext.Select(Type type)
        {
            return new ContextSelect(this.GetSelectQuery(type), type);
        }

        IContextCount IEntityContext.Count(Type type)
        {
            return new ContextCount(this.GetCountQuery(type));
        }

        private class ExistingTable : IEntityTable
        {
            public string Name { get; set; }
            public Type EntityType { get; set; }
        }

        IEntityTable[] IEntityContext.ExistingTables()
        {
            var tables = GetSchema();
            var r = new ExistingTable[tables.Length];
            var entities = AllEntities.Inst.All();
            for (int i = 0; i < tables.Length; i++)
            {
                r[i] = new ExistingTable()
                {
                    Name = tables[i],
                    EntityType = Array.Find(entities, e => e.TableDescriptor.Name == tables[i])?.EntityType
                };
            }

            return r;
        }
    }
}