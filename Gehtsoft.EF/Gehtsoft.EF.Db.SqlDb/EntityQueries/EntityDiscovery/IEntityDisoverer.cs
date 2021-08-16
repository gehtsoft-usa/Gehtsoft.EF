using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public interface IEntityDisoverer
    {
        TableDescriptor Discover(AllEntities entities, Type type);
    }
}
