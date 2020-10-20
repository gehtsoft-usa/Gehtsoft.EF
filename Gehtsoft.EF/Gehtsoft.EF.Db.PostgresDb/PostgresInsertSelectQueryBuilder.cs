using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.PostgresDb
{
    class PostgresInsertSelectQueryBuilder : InsertSelectQueryBuilder
    {
        private bool mHasAutoId = false;

        public PostgresInsertSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor descriptor, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement = false) : base(specifics, descriptor, selectQuery, ignoreAutoIncrement)
        {
            if (ignoreAutoIncrement)
            {
                bool hasAutoId = false;
                foreach (TableDescriptor.ColumnInfo column in descriptor)
                {
                    if (column.Autoincrement == true && column.PrimaryKey == true)
                    {
                        hasAutoId = true;
                        break;
                    }
                }

                mHasAutoId = hasAutoId;
            }
        }

        protected override string BuildQuery(StringBuilder leftSide, TableDescriptor.ColumnInfo autoIncrement)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(base.BuildQuery(leftSide, autoIncrement));
            if (autoIncrement != null)
                builder.Append($"; SELECT last_value from {mTable.Name}_{autoIncrement.Name}_seq;");
            else if (mHasAutoId)
                builder.Append($"; SELECT pg_catalog.setval(pg_get_serial_sequence('{mTable.Name}', '{mTable.PrimaryKey.Name.ToLower()}'), (SELECT MAX({mTable.PrimaryKey.Name.ToLower()}) FROM {mTable.Name}));");
            return builder.ToString();
        }
    }
}
