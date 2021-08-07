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

        public IEntityQuery DropEntity(Type type)
        {
            return new ContextQuery(this.GetDeleteListQuery(type));
        }

        public IEntityQuery CreateEntity(Type type)
        {
            return new ContextQuery(this.GetCreateListQuery(type));
        }

        public IModifyEntityQuery InsertEntity(Type type, bool createKey)
        {
            return new ContextModifyQuery(this.GetInsertEntityQuery(type));
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
            return new ContextQueryWithCondition(this.GetDeleteMultiEntityQuery(type));
        }

        public IContextSelect Select(Type type)
        {
            return new ContextSelect(this.GetSelectQuery(type), type);
        }

        public IContextCount Count(Type type)
        {
            return new ContextCount(this.GetCountQuery(type));
        }

        class ExistingTable : IEntityTable
        {
            public string Name { get; set; }
            public Type EntityType { get; set; }
        }

        public IEntityTable[] ExistingTables()
        {
            var tables = GetSchema();
            var r = new ExistingTable[tables.Length];
            var entities = AllEntities.Inst.All();
            for (int i = 0; i < tables.Length; i++)
            {
                r[i] = new ExistingTable()
                {
                    Name = tables[i],
                    EntityType = entities.FirstOrDefault(e => e.TableDescriptor.Name == tables[i])?.EntityType
                };
            }

            return r;
        }
    }
}