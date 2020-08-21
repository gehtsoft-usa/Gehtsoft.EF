using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MysqlDb
{
    class MysqlHierarchicalSelectQueryBuilder : HierarchicalSelectQueryBuilder
    {
        internal MysqlHierarchicalSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table, TableDescriptor.ColumnInfo parentReferenceColumn, string rootParameterName) : base(specifics, table, parentReferenceColumn, rootParameterName)
        {

        }

        public override void PrepareQuery()
        {
            throw new EfSqlException(EfExceptionCode.FeatureNotSupported);
        }
    }
}
