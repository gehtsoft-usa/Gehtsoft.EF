using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.OracleDb
{
    internal class OracleHierarchicalSelectQueryBuilder : HierarchicalSelectQueryBuilder
    {
        internal OracleHierarchicalSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table, TableDescriptor.ColumnInfo parentReferenceColumn, string rootParameterName) : base(specifics, table, parentReferenceColumn, rootParameterName)
        {
        }

        public override void PrepareQuery()
        {
            string anchor;

            if (mRootParameterName == null)
                anchor = $"START WITH {mReferenceColumn.Name} IS NULL";
            else
                anchor = $"START WITH {mPrimaryKey.Name}={mSpecifics.ParameterInQueryPrefix}{mRootParameterName}";

            if (IdOnlyMode)
                mQuery = $"SELECT {mPrimaryKey.Name} AS id FROM {mDescriptor.Name} {anchor} CONNECT BY PRIOR {mPrimaryKey.Name} = {mReferenceColumn.Name}";
            else
                mQuery = $"SELECT {mPrimaryKey.Name} AS id, {mReferenceColumn.Name} AS parent, LEVEL FROM {mDescriptor.Name} {anchor} CONNECT BY PRIOR {mPrimaryKey.Name} = {mReferenceColumn.Name}";
        }
    }
}
