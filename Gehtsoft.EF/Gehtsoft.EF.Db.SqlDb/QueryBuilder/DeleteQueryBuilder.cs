using System.Text;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public class DeleteQueryBuilder : SingleTableQueryWithWhereBuilder
    {
        public DeleteQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table) : base(specifics, table)
        {
        }

        public void DeleteById()
        {
            Where.Property(mDescriptor.PrimaryKey).Is(CmpOp.Eq).Parameter(mDescriptor.PrimaryKey.Name);
        }

        public override void PrepareQuery()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("DELETE FROM ");
            builder.Append(mDescriptor.Name);
            if (!Where.IsEmpty)
            {
                builder.Append(" WHERE ");
                builder.Append(Where);
            }
            mQuery = builder.ToString();
        }
    }
}
