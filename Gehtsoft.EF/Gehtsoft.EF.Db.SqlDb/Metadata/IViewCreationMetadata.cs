using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using System;
using System.Text;

namespace Gehtsoft.EF.Db.SqlDb.Metadata
{
    public interface IViewCreationMetadata
    {
        SelectQueryBuilder GetSelectQuery(SqlDbConnection connection);
    }
}
