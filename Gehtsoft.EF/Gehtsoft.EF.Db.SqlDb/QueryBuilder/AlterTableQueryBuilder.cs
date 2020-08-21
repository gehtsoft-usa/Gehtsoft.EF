using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public abstract class AlterTableQueryBuilder
    {
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

        public string[] GetQueries()
        {
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

        protected virtual void HandleAddColumn(TableDescriptor.ColumnInfo column)
        {
            HandleCreateQuery(column);   
            HandleAfterCreateQuery(column);

        }

        protected virtual void HandleDropColumn(TableDescriptor.ColumnInfo column)
        {
            HandlePreDropQuery(column);
            HandleDropQuery(column);
            
        }

        protected virtual string GetDDL(TableDescriptor.ColumnInfo column)
        {
            StringBuilder builder = new StringBuilder();
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
            if (column.ForeignKey && column.ForeignTable != column.Table)
                builder.Append($" FOREIGN KEY REFERENCES {column.ForeignTable.Name}({column.ForeignTable.PrimaryKey.Name})");
            
            
            return builder.ToString();

        }

        protected virtual bool NeedIndex(TableDescriptor.ColumnInfo column)
        {
            return (column.Sorted || (column.ForeignKey && column.ForeignTable == column.Table));
        }

        protected virtual void HandlePreDropQuery(TableDescriptor.ColumnInfo column)
        {
            return ;
        }

        protected virtual void HandleDropQuery(TableDescriptor.ColumnInfo column)
        {
            mQueries.Add($"ALTER TABLE {mDescriptor.Name} DROP COLUMN {column.Name}");
        }

        protected virtual void HandleCreateQuery(TableDescriptor.ColumnInfo column)
        {
            mQueries.Add($"ALTER TABLE {mDescriptor.Name} ADD COLUMN {GetDDL(column)}");
        }

        protected virtual void HandleAfterCreateQuery(TableDescriptor.ColumnInfo column)
        {
            if (NeedIndex(column))
                mQueries.Add($"CREATE INDEX {mDescriptor.Name}_{column.Name} ON {mDescriptor.Name}({column.Name})");
        }
    }
}