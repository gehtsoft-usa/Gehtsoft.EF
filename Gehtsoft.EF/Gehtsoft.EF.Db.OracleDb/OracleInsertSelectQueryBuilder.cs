using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using System.Text;

namespace Gehtsoft.EF.Db.OracleDb
{
    class OracleInsertSelectQueryBuilder : InsertSelectQueryBuilder
    {
        public OracleInsertSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement = false) : base(specifics, table, selectQuery, ignoreAutoIncrement)
        {
        }

        protected override bool HasExpressionForAutoincrement => true;

        protected override string ExpressionForAutoincrement(TableDescriptor.ColumnInfo autoIncrement)
        {
            return $"{mTable.Name}_{autoIncrement.Name}.nextval";
        }
    }
}
