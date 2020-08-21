using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MssqlDb
{
    class MssqlHierarchicalSelectQueryBuilder : HierarchicalSelectQueryBuilder
    {
        private string mWith;
        private string mSelect;

        public string With => mWith;
        public string Select => mSelect;

        internal MssqlHierarchicalSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table, TableDescriptor.ColumnInfo parentReferenceColumn, string rootParameterName) : base(specifics, table, parentReferenceColumn, rootParameterName)
        {
            
        }

        public override void PrepareQuery()
        {
            string rootParameterName = mRootParameterName;
            string keyfield = mPrimaryKey.Name;
            string parentreference = mReferenceColumn.Name;
            string table = mDescriptor.Name;


            string anchor = rootParameterName != null ? $"{keyfield}={mSpecifics.ParameterInQueryPrefix}{rootParameterName}" : $"{parentreference} IS NULL";

            string alias1 = NextAlias;
            string alias2 = NextAlias;

            if (IdOnlyMode)
            {
                mWith = $@"WITH {alias1} (id)
                        AS
                         (
                            SELECT {alias2}.{keyfield} AS id
                            FROM {table} AS {alias2}
                            WHERE {anchor}
                            UNION ALL
                            SELECT {alias2}.{keyfield} AS id
                            FROM {table} AS {alias2}
                            INNER JOIN {alias1} {alias2}tree 
                                  ON {alias2}.{parentreference} = {alias2}tree.{keyfield}
                          )";
                mSelect = $"SELECT distinct id FROM {alias1}";
                mQuery = mWith + " " + mSelect;

            }
            else
            {
                mWith = $@"WITH {alias1} (id, parent, level)
                        AS
                         (
                            SELECT {alias2}.{keyfield} AS id, {alias2}.{parentreference} AS parent, 1 AS level 
                            FROM {table} AS {alias2}
                            WHERE {anchor}
                            UNION ALL
                            SELECT {alias2}.{keyfield} AS id, {alias2}.{parentreference} AS parent, level + 1 AS level 
                            FROM {table} AS {alias2}
                            INNER JOIN {alias1} {alias2}tree 
                                  ON {alias2}.{parentreference} = {alias2}tree.{keyfield}
                          )";
                mSelect = $"SELECT * FROM {alias1}";
                mQuery = mWith + " " + mSelect;
            }
        }
    }
}
