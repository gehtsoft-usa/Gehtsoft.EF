using System.Linq;
using System.Text;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// The query builder for `CREATE INDEX` command.
    ///
    /// Use <see cref="SqlDbConnection.GetCreateIndexBuilder(TableDescriptor, CompositeIndex)"/> to create an instance of this object.
    /// </summary>
    public class CreateIndexBuilder : AQueryBuilder
    {
        protected string mQuery;
        protected TableDescriptor mDescriptor;
        protected CompositeIndex mIndex;

        [DocgenIgnore]
        internal protected CreateIndexBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table, CompositeIndex index)
            : base(specifics)
        {
            mDescriptor = table;
            mIndex = index;
        }

        [DocgenIgnore]
        public override string Query => mQuery;

        [DocgenIgnore]
        public override void PrepareQuery()
        {
            if (mQuery != null)
                return;

            if (!mSpecifics.SupportFunctionsInIndexes && mIndex.Any(f => f.Function != null))
            {
                if (mIndex.FailIfUnsupported)
                    throw new EfSqlException(EfExceptionCode.FeatureNotSupported);
                else
                {
                    mQuery = "";
                    return;
                }
            }

            StringBuilder builder = new StringBuilder();

            builder.Append(mSpecifics.PreBlock);
            builder.Append(mSpecifics.PreQueryInBlock);

            builder
                .Append("CREATE INDEX ")
                .Append(mDescriptor.Name)
                .Append('_')
                .Append(mIndex.Name)
                .Append(" ON ")
                .Append(mDescriptor.Name)
                .Append('(');
            HandleCompositeIndexColumns(builder, mIndex);
            builder.Append(")");

            if (mSpecifics.TerminateWithSemicolon)
                builder.Append(';');

            builder.Append(mSpecifics.PostQueryInBlock);
            builder.Append(mSpecifics.PostBlock);

            mQuery = builder.ToString();
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