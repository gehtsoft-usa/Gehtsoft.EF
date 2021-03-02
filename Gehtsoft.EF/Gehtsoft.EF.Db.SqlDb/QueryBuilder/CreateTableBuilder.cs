using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public class CreateTableBuilder : AQueryBuilder
    {
        protected TableDescriptor mDescriptor;
        protected string mQuery;
        public override string Query => mQuery;

        public CreateTableBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor tableDescriptor) : base(specifics)
        {
            mSpecifics = specifics;
            mDescriptor = tableDescriptor;
        }

        public override void PrepareQuery()
        {
            if (mQuery != null)
                return;

            StringBuilder builder = new StringBuilder();
            bool first = true;

            builder.Append(mSpecifics.PreBlock);
            builder.Append(mSpecifics.PreQueryInBlock);
            builder.Append($"CREATE TABLE {mDescriptor.Name} (");

            foreach (TableDescriptor.ColumnInfo column in mDescriptor)
            {
                if (!first)
                    builder.Append(',');
                else
                    first = false;

                HandleColumnDDL(builder, column);
            }

            foreach (TableDescriptor.ColumnInfo column in mDescriptor)
            {
                HandlePostfixDDL(builder, column);
            }

            builder.Append(')');
            if (mSpecifics.TerminateWithSemicolon)
                builder.Append(';');
            builder.Append(mSpecifics.PostQueryInBlock);

            foreach (TableDescriptor.ColumnInfo column in mDescriptor)
                HandleAfterQuery(builder, column);

            if (mDescriptor.Metadata != null && mDescriptor.Metadata is ICompositeIndexMetadata compositeIndex)
            {
                foreach (var index in compositeIndex.Indexes)
                    HandleCompositeIndex(builder, index);
            }

            builder.Append(mSpecifics.PostBlock);

            mQuery = builder.ToString();
        }

        protected virtual void HandleColumnDDL(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            string type = mSpecifics.TypeName(column.DbType, column.Size, column.Precision, column.Autoincrement);
            builder.Append($"{column.Name} {type}");
            if (column.PrimaryKey)
                builder.Append($" PRIMARY KEY");
            if (!column.Nullable)
                builder.Append($" NOT NULL");
            if (column.Unique)
                builder.Append($" UNIQUE");
            if (column.DefaultValue != null)
                builder.Append($" DEFAULT {mSpecifics.FormatValue(column.DefaultValue)}");
        }

        protected virtual void HandlePostfixDDL(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            if (column.ForeignKey && column.ForeignTable != column.Table)
                builder.Append($", FOREIGN KEY ({column.Name}) REFERENCES {column.ForeignTable.Name}({column.ForeignTable.PrimaryKey.Name})");
        }

        protected virtual bool NeedIndex(TableDescriptor.ColumnInfo column)
        {
            return (column.Sorted || (column.ForeignKey && column.ForeignTable == column.Table));
        }

        protected virtual void HandleAfterQuery(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            if (NeedIndex(column))
            {
                builder.Append("\r\n");
                builder.Append(mSpecifics.PreQueryInBlock);
                builder.Append($"CREATE INDEX {mDescriptor.Name}_{column.Name} ON {mDescriptor.Name}({column.Name})");
                if (mSpecifics.TerminateWithSemicolon)
                    builder.Append(';');

                builder.Append(mSpecifics.PostQueryInBlock);
            }
        }

        protected virtual void HandleCompositeIndex(StringBuilder builder, CompositeIndex index)
        {
            builder.Append("\r\n");
            builder.Append(mSpecifics.PreQueryInBlock);

            builder.Append($"CREATE INDEX {mDescriptor.Name}_{index.Name} ON {mDescriptor.Name}(");
            HandleCompositeIndexColumns(builder, index);
            builder.Append(")");
            if (mSpecifics.TerminateWithSemicolon)
                builder.Append(';');
            builder.Append(mSpecifics.PostQueryInBlock);

        }

        protected virtual void HandleCompositeIndexColumns(StringBuilder builder, CompositeIndex index)
        {
            for (int i = 0; i < index.Fields.Count; i++)
            {
                var field = index.Fields[i];
                if (i > 0)
                    builder.Append(", ");
                builder.Append(field.Name);
                if (field.Direction == SortDir.Desc)
                    builder.Append(" DESC");
            }
        }
    }
}