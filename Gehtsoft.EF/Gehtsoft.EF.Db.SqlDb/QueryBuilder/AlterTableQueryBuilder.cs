using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// The query builder for the `ALTER TABLE` command.
    ///
    /// Use <see cref="SqlDbConnection.GetAlterTableQueryBuilder"/> to create an instance of this object.
    ///
    /// Please note that this builder is the only builder which is not derived from <see cref="AQueryBuilder"/>. Because
    /// of the specified nature of `ALTER TABLE` command, it returns a sequence of the commands instead of
    /// single query.
    /// </summary>
    public class AlterTableQueryBuilder
    {
        protected TableDdlBuilder DdlBuilder { get; set; }
        protected TableDescriptor mDescriptor;
        protected TableDescriptor.ColumnInfo[] mAddColumns, mDropColumns;
        protected List<string> mQueries;
        protected SqlDbLanguageSpecifics mSpecifics;
        private bool mPrepared;

        [DocgenIgnore]
        internal protected AlterTableQueryBuilder(SqlDbLanguageSpecifics specifics)
        {
            mSpecifics = specifics;
        }

        /// <summary>
        /// Sets the table to alter.
        /// </summary>
        /// <param name="descriptor">The table descriptor</param>
        /// <param name="addColumns">The list of columns to add to the table</param>
        /// <param name="dropColumns">The list of columns to drop</param>
        public virtual void SetTable(TableDescriptor descriptor, TableDescriptor.ColumnInfo[] addColumns, TableDescriptor.ColumnInfo[] dropColumns)
        {
            mDescriptor = descriptor;
            mAddColumns = addColumns;
            mDropColumns = dropColumns;
            mPrepared = false;
            mQueries = new List<string>();
        }

        protected virtual TableDdlBuilder CreateDdlBuilder() => new TableDdlBuilder(mSpecifics);

        /// <summary>
        /// Returns whether the framework emits a standalone single-column index for the specified
        /// column when the table is created (e.g. a `Sorted` column, or a foreign key under the
        /// conditions where its index is not created automatically). Delegates to the driver's
        /// <see cref="TableDdlBuilder.NeedIndex(TableDescriptor.ColumnInfo)"/> so the answer matches
        /// what table creation actually produces on this dialect.
        /// </summary>
        /// <param name="column">The column to test.</param>
        public bool NeedIndex(TableDescriptor.ColumnInfo column)
        {
            if (DdlBuilder == null)
                DdlBuilder = CreateDdlBuilder();
            return DdlBuilder.NeedIndex(column);
        }

        /// <summary>
        /// Returns the statement(s) that create one spatial index on an existing geometry column,
        /// without adding or dropping any column. Used by the schema catalogue to reconcile a spatial
        /// index that appeared on an otherwise-unchanged geometry column. The column must carry its
        /// geometry metadata (SRID etc.); the index carries its own name and bounding box.
        /// </summary>
        public string[] GetCreateSpatialIndexQueries(TableDescriptor descriptor, TableDescriptor.ColumnInfo column, Metadata.SpatialIndexDefinition index)
        {
            if (DdlBuilder == null)
                DdlBuilder = CreateDdlBuilder();
            if (column.Table == null)
                column.Table = descriptor;
            var queries = new List<string>();
            DdlBuilder.CollectCreateSpatialIndex(queries, column, index);
            return queries.ToArray();
        }

        /// <summary>
        /// Returns the statement(s) that drop one spatial index from an existing geometry column,
        /// without adding or dropping any column. The counterpart of
        /// <see cref="GetCreateSpatialIndexQueries"/>.
        /// </summary>
        public string[] GetDropSpatialIndexQueries(TableDescriptor descriptor, TableDescriptor.ColumnInfo column, Metadata.SpatialIndexDefinition index)
        {
            if (DdlBuilder == null)
                DdlBuilder = CreateDdlBuilder();
            if (column.Table == null)
                column.Table = descriptor;
            var queries = new List<string>();
            DdlBuilder.CollectDropSpatialIndex(queries, column, index);
            return queries.ToArray();
        }

        /// <summary>
        /// Get queries to perform requested operations.
        ///
        /// The queries should be executed in the same order as they returned.
        /// </summary>
        /// <returns></returns>
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
            if (column.Geometry != null)
            {
                var indexes = column.Geometry.Indexes;
                for (int i = 0; i < indexes.Count; i++)
                    DdlBuilder.CollectDropSpatialIndex(mQueries, column, indexes[i]);
                DdlBuilder.CollectUnregisterGeometryColumn(mQueries, column);
            }
        }

        protected virtual void HandleDropQuery(TableDescriptor.ColumnInfo column)
        {
            mQueries.Add($"ALTER TABLE {mDescriptor.Name} DROP COLUMN {column.Name}");
        }

        protected virtual string AddColumnKeyword => " ADD COLUMN ";

        protected virtual void HandleCreateQuery(TableDescriptor.ColumnInfo column)
        {
            if (column.Geometry != null)
            {
                HandleCreateGeometryColumn(column);
                return;
            }

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
            if (column.Geometry != null)
            {
                var indexes = column.Geometry.Indexes;
                for (int i = 0; i < indexes.Count; i++)
                    DdlBuilder.CollectCreateSpatialIndex(mQueries, column, indexes[i]);
                return;
            }

            if (DdlBuilder.NeedIndex(column))
                mQueries.Add($"CREATE INDEX {mDescriptor.Name}_{column.Name} ON {mDescriptor.Name}({column.Name})");
        }

        /// <summary>
        /// Emits the ALTER-time statements that add a geometry column to an existing table: the
        /// <c>ALTER TABLE ... ADD</c> for the column itself (unless the dialect adds it out-of-band, e.g.
        /// SpatiaLite via <c>AddGeometryColumn</c>) followed by the spatial-catalog registration. The
        /// spatial indexes are emitted separately by <see cref="HandleAfterCreateQuery"/>.
        /// </summary>
        protected virtual void HandleCreateGeometryColumn(TableDescriptor.ColumnInfo column)
        {
            if (!DdlBuilder.SkipInlineColumn(column))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("ALTER TABLE ")
                    .Append(mDescriptor.Name)
                    .Append(' ')
                    .Append(AddColumnKeyword)
                    .Append(' ');
                DdlBuilder.HandleColumnDDL(sb, column, true);
                mQueries.Add(sb.ToString());
            }

            DdlBuilder.CollectRegisterGeometryColumn(mQueries, column);
        }
    }
}