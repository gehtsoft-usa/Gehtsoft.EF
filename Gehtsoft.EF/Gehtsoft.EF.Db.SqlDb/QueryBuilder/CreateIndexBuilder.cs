using System.Linq;
using System.Text;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public class CreateIndexBuilder : AQueryBuilder
    {
        protected string mQuery;
        protected TableDescriptor mDescriptor;
        protected CompositeIndex mIndex;

        public CreateIndexBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table, CompositeIndex index)
            : base(specifics)
        {
            mDescriptor = table;
            mIndex = index;
        }

        public override string Query => mQuery;

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
                if (i > 0)
                    builder.Append(", ");
                if (field.Function != null)
                    builder.Append(mSpecifics.GetSqlFunction(field.Function.Value, new string[] { field.Name }));
                else
                    builder.Append(field.Name);
                if (field.Direction == SortDir.Desc)
                    builder.Append(" DESC");
            }
        }
    }
}