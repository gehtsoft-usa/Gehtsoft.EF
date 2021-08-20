using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public class AlterTableQueryBuilder
    {
        protected TableDdlBuilder DdlBuilder { get; set; }
        protected TableDescriptor mDescriptor;
        protected TableDescriptor.ColumnInfo[] mAddColumns, mDropColumns;
        protected List<string> mQueries;
        protected SqlDbLanguageSpecifics mSpecifics;
        private bool mPrepared;

        protected AlterTableQueryBuilder(SqlDbLanguageSpecifics specifics)
        {
            mSpecifics = specifics;
        }

        public virtual void SetTable(TableDescriptor descriptor, TableDescriptor.ColumnInfo[] addColumns, TableDescriptor.ColumnInfo[] dropColumns)
        {
            mDescriptor = descriptor;
            mAddColumns = addColumns;
            mDropColumns = dropColumns;
            mPrepared = false;
            mQueries = new List<string>();
        }

        protected virtual TableDdlBuilder CreateDdlBuilder() => new TableDdlBuilder(mSpecifics, mDescriptor);

        public string[] GetQueries()
        {
            if (DdlBuilder == null)
                DdlBuilder = CreateDdlBuilder();

            if (mDescriptor == null || (mAddColumns == null && mDropColumns == null))
                return new string[] { };

            if (!mPrepared)
                Prepare();

            return mQueries.ToArray();
        }

        protected virtual void Prepare()
        {
            if (mDropColumns != null)
            {
                foreach (TableDescriptor.ColumnInfo column in mDropColumns)
                {
                    if (column.Table == null)
                        column.Table = mDescriptor;
                    HandleDropColumn(column);
                }
            }

            if (mAddColumns != null)
            {
                foreach (TableDescriptor.ColumnInfo column in mAddColumns)
                {
                    if (column.Table == null)
                        column.Table = mDescriptor;
                    HandleAddColumn(column);
                }
            }

            mPrepared = true;
        }

        private void HandleAddColumn(TableDescriptor.ColumnInfo column)
        {
            HandleCreateQuery(column);
            HandleAfterCreateQuery(column);
        }

        private void HandleDropColumn(TableDescriptor.ColumnInfo column)
        {
            HandlePreDropQuery(column);
            HandleDropQuery(column);
        }

        protected virtual void HandlePreDropQuery(TableDescriptor.ColumnInfo column)
        {
        }

        protected virtual void HandleDropQuery(TableDescriptor.ColumnInfo column)
        {
            mQueries.Add($"ALTER TABLE {mDescriptor.Name} DROP COLUMN {column.Name}");
        }

        protected virtual string AddColumnKeyword => " ADD COLUMN ";

        protected virtual void HandleCreateQuery(TableDescriptor.ColumnInfo column)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("ALTER TABLE ")
                .Append(mDescriptor.Name)
                .Append(' ')
                .Append(AddColumnKeyword)
                .Append(' ');

            DdlBuilder.HandleColumnDDL(sb, column, true);
            mQueries.Add(sb.ToString());

            sb = new StringBuilder();
            sb.Append("ALTER TABLE ")
                .Append(mDescriptor.Name)
                .Append(" ADD ");
            var l = sb.Length;
            DdlBuilder.HandlePostfixDDL(sb, column, true);
            if (sb.Length > l)
                mQueries.Add(sb.ToString());
        }

        protected virtual void HandleAfterCreateQuery(TableDescriptor.ColumnInfo column)
        {
            if (DdlBuilder.NeedIndex(column))
                mQueries.Add($"CREATE INDEX {mDescriptor.Name}_{column.Name} ON {mDescriptor.Name}({column.Name})");
        }
    }
}