using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqliteDb
{
    class SqliteHierarchicalSelectQueryBuilder : HierarchicalSelectQueryBuilder
    {
        internal SqliteHierarchicalSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table, TableDescriptor.ColumnInfo parentReferenceColumn, string rootParameterName) : base(specifics, table, parentReferenceColumn, rootParameterName)
        {

        }

        public override void PrepareQuery()
        {
            string rootParameterName = mRootParameterName;
            string keyfield = mPrimaryKey.Name;
            string parentreference = mReferenceColumn.Name;
            string table = mDescriptor.Name;

            string anchor;
            string alias1 = NextAlias;
            string alias2 = NextAlias;
            string alias3 = NextAlias;

            if (rootParameterName != null)
                anchor = $"{alias2}.{keyfield} = {mSpecifics.ParameterInQueryPrefix}{rootParameterName}";
            else
                anchor = $"{alias2}.{parentreference} IS NULL";

            if (IdOnlyMode)
            {
                mQuery = $@"WITH RECURSIVE {alias1} AS
                      (SELECT {alias2}.{keyfield} AS id FROM {table} {alias2} WHERE {anchor}
                        UNION ALL 
                       SELECT {alias3}.{keyfield} AS id FROM {table} {alias3}
                              JOIN {alias1} ON {alias3}.{parentreference} = {alias1}.{keyfield})
                         SELECT DISTINCT id FROM {alias1}";
            }
            else
            {
                mQuery = $@"WITH RECURSIVE {alias1} AS
                      (SELECT {alias2}.{keyfield} AS id, {alias2}.{parentreference} AS parent, 1 as level FROM {table} {alias2} WHERE {anchor}
                        UNION ALL 
                       SELECT {alias3}.{keyfield} AS id, {alias3}.{parentreference} AS parent, {alias1}.level + 1 as level FROM {table} {alias3}
                              JOIN {alias1} ON {alias3}.{parentreference} = {alias1}.{keyfield})
                         SELECT * FROM {alias1}";
            }
        }
    }
}
