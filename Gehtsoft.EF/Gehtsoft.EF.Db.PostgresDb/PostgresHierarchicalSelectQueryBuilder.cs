using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.PostgresDb
{
    internal class PostgresHierarchicalSelectQueryBuilder : HierarchicalSelectQueryBuilder
    {
        internal PostgresHierarchicalSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table, TableDescriptor.ColumnInfo parentReferenceColumn, string rootParameterName) : base(specifics, table, parentReferenceColumn, rootParameterName)
        {
        }

        public override void PrepareQuery()
        {
            string rootParameterName = mRootParameterName;
            string keyfield = mPrimaryKey.Name;
            string parentreference = mReferenceColumn.Name;
            string table = mDescriptor.Name;

            string anchor;
            if (rootParameterName != null)
                anchor = $"{keyfield}={mSpecifics.ParameterInQueryPrefix}{rootParameterName}";
            else
                anchor = $"{parentreference} IS NULL";

            string alias1 = NextAlias;
            string alias2 = NextAlias;
            string alias3 = NextAlias;

            if (IdOnlyMode)
            {
                mQuery = $@"WITH RECURSIVE {alias3} (id) AS (
                              SELECT {alias1}.{keyfield} AS id 
                                FROM {table} {alias1} WHERE {anchor}
                              UNION ALL
                              SELECT {alias2}.{keyfield} AS id 
                                FROM {table} {alias2}, {alias3} 
                                WHERE {alias3}.{keyfield} = {alias2}.{parentreference}
                            ) SELECT DISTINCT id FROM  {alias3}";
            }
            else
            {
                mQuery = $@"WITH RECURSIVE {alias3} (id, parent, level) AS (
                              SELECT {alias1}.{keyfield} AS id, {alias1}.{parentreference} AS parent, 1 AS level FROM {table} {alias1} WHERE {anchor}
                              UNION ALL
                              SELECT {alias2}.{keyfield} AS id, {alias2}.{parentreference} AS parent, level + 1 AS level 
                                FROM {table} {alias2}, {alias3} 
                                WHERE {alias3}.{keyfield} = {alias2}.{parentreference}
                            ) SELECT * FROM  {alias3}";
            }
        }
    }
}
