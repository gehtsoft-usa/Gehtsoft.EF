using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.OracleDb
{
    internal class OracleAlterTableQueryBuilder : AlterTableQueryBuilder
    {
        public OracleAlterTableQueryBuilder(SqlDbLanguageSpecifics specifics) : base(specifics)
        {
        }

        protected override string GetDDL(TableDescriptor.ColumnInfo column)
        {
            StringBuilder builder = new StringBuilder();
            string type = mSpecifics.TypeName(column.DbType, column.Size, column.Precision, column.Autoincrement);
            builder.Append(column.Name).Append(' ').Append(type);
            if (column.PrimaryKey)
                builder.Append(" PRIMARY KEY");
            if (!column.Nullable)
                builder.Append(" NOT NULL");
            if (column.Unique)
                builder.Append(" UNIQUE");
            if (column.DefaultValue != null)
                builder.Append(" DEFAULT ").Append(mSpecifics.FormatValue(column.DefaultValue));
            return builder.ToString();
        }

        protected override void HandleCreateQuery(TableDescriptor.ColumnInfo column)
        {
            mQueries.Add($"ALTER TABLE {mDescriptor.Name} ADD {GetDDL(column)}");
        }

        protected override void HandleAfterCreateQuery(TableDescriptor.ColumnInfo column)
        {
            if (column.Autoincrement)
                mQueries.Add($"CREATE SEQUENCE {mDescriptor.Name}_{column.Name} START WITH 1 INCREMENT BY 1 MINVALUE 1");
            if (column.ForeignKey)
                mQueries.Add($"ALTER TABLE {mDescriptor.Name} ADD CONSTRAINT  {mDescriptor.Name}_{column.Name}_fk FOREIGN KEY({column.Name}) REFERENCES {column.ForeignTable.Name}({column.ForeignTable.PrimaryKey.Name})");
            base.HandleAfterCreateQuery(column);
        }

        protected override void HandlePreDropQuery(TableDescriptor.ColumnInfo column)
        {
            base.HandlePreDropQuery(column);
            if (column.Autoincrement)
                mQueries.Add($"DROP SEQUENCE {mDescriptor.Name}_{column.Name}");
        }

        protected override void HandleDropQuery(TableDescriptor.ColumnInfo column)
        {
            mQueries.Add($"ALTER TABLE {mDescriptor.Name} DROP COLUMN {column.Name}");
        }
    }
}
