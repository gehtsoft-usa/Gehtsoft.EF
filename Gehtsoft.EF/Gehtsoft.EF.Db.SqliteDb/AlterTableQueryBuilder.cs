using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Db.SqliteDb
{
    public class SqliteAlterTableQueryBuilder : AlterTableQueryBuilder
    {
        public SqliteAlterTableQueryBuilder(SqlDbLanguageSpecifics specifics) : base(specifics)
        {
        }

        protected override string GetDDL(TableDescriptor.ColumnInfo column)
        {
            StringBuilder builder = new StringBuilder();
            string type = mSpecifics.TypeName(column.DbType, column.Size, column.Precision, column.Autoincrement);
            builder.Append(column.Name).Append(' ').Append(type);
            if (column.PrimaryKey)
                throw new EfSqlException(EfExceptionCode.FeatureNotSupported);
            if (column.Autoincrement)
                builder.Append(" AUTOINCREMENT");
            if (column.DefaultValue != null)
                builder.Append(" DEFAULT ").Append(mSpecifics.FormatValue(column.DefaultValue));
            if (!column.Nullable)
                throw new EfSqlException(EfExceptionCode.FeatureNotSupported);
            if (column.Unique)
                throw new EfSqlException(EfExceptionCode.FeatureNotSupported);
            return builder.ToString();
        }

        protected override void HandleDropQuery(TableDescriptor.ColumnInfo column)
        {
            throw new EfSqlException(EfExceptionCode.FeatureNotSupported);
        }

        protected override void HandleCreateQuery(TableDescriptor.ColumnInfo column)
        {
            mQueries.Add($"ALTER TABLE {column.Table.Name} ADD COLUMN {GetDDL(column)}");
        }

        protected override bool NeedIndex(TableDescriptor.ColumnInfo column)
        {
            return column.ForeignKey || base.NeedIndex(column);
        }
    }
}
