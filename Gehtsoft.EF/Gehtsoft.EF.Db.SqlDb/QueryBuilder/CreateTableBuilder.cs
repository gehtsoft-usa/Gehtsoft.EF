﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// The query builder for `CREATE TABLE` command.
    ///
    /// Use <see cref="SqlDbConnection.GetCreateTableBuilder(TableDescriptor)"/> to create an instance of this object.
    /// </summary>
    public class CreateTableBuilder : AQueryBuilder
    {
        protected virtual TableDdlBuilder DdlBuilder { get; set; }
        protected TableDescriptor mDescriptor;
        protected string mQuery;

        [DocgenIgnore]
        public override string Query => mQuery;

        internal protected CreateTableBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor tableDescriptor) : base(specifics)
        {
            mSpecifics = specifics;
            mDescriptor = tableDescriptor;
        }

        [DocgenIgnore]

        public override void PrepareQuery()
        {
            if (DdlBuilder == null)
                DdlBuilder = new TableDdlBuilder(mSpecifics, mDescriptor);

            if (mQuery != null)
                return;

            StringBuilder builder = new StringBuilder();
            bool first = true;

            builder.Append(mSpecifics.PreBlock);
            builder.Append(mSpecifics.PreQueryInBlock);
            builder.Append("CREATE TABLE ").Append(mDescriptor.Name).Append(" (");

            foreach (TableDescriptor.ColumnInfo column in mDescriptor)
            {
                if (!first)
                    builder.Append(',');
                else
                    first = false;

                DdlBuilder.HandleColumnDDL(builder, column, false);
            }

            foreach (TableDescriptor.ColumnInfo column in mDescriptor)
            {
                DdlBuilder.HandlePostfixDDL(builder, column, false);
            }

            builder.Append(')');
            if (mSpecifics.TerminateWithSemicolon)
                builder.Append(';');
            builder.Append(mSpecifics.PostQueryInBlock);

            foreach (TableDescriptor.ColumnInfo column in mDescriptor)
                DdlBuilder.HandleAfterQuery(builder, column);

            if (mDescriptor.Metadata is ICompositeIndexMetadata compositeIndex)
            {
                foreach (var index in compositeIndex.Indexes)
                    HandleCompositeIndex(builder, index);
            }

            builder.Append(mSpecifics.PostBlock);

            mQuery = builder.ToString();
        }

        protected virtual void HandleCompositeIndex(StringBuilder builder, CompositeIndex index)
        {
            if (!mSpecifics.SupportFunctionsInIndexes && index.Any(f => f.Function != null))
            {
                if (index.FailIfUnsupported)
                    throw new EfSqlException(EfExceptionCode.FeatureNotSupported);
                else
                    return;
            }

            builder.Append("\r\n");
            builder.Append(mSpecifics.PreQueryInBlock);

            builder
                .Append("CREATE INDEX ")
                .Append(mDescriptor.Name)
                .Append('_')
                .Append(index.Name)
                .Append(" ON ")
                .Append(mDescriptor.Name)
                .Append('(');
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
                var name = mDescriptor.FirstOrDefault(c => c.ID == field.Name || c.Name == field.Name)?.Name ?? field.Name;
                if (i > 0)
                    builder.Append(", ");
                if (field.Function != null)
                    builder.Append(mSpecifics.GetSqlFunction(field.Function.Value, new string[] { name }));
                else
                    builder.Append(name);
                if (field.Direction == SortDir.Desc)
                    builder.Append(" DESC");
            }
        }
    }
}