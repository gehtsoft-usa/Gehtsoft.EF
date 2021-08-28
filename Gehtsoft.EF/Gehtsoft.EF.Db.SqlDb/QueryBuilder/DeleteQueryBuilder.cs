using System.Text;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// The query builder for `DELETE` command.
    ///
    /// Use <see cref="SqlDbConnection.GetDeleteQueryBuilder(TableDescriptor)"/> to create an instance of this object.
    /// </summary>
    public class DeleteQueryBuilder : SingleTableQueryWithWhereBuilder
    {
        [DocgenIgnore]
        internal protected DeleteQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table) : base(specifics, table)
        {
        }

        /// <summary>
        /// Creates a condition to delete an entity by ID.
        ///
        /// The parameter name to set the ID is named as the primary key column.
        /// </summary>
        public void DeleteById()
        {
            Where.Property(mDescriptor.PrimaryKey).Is(CmpOp.Eq).Parameter(mDescriptor.PrimaryKey.Name);
        }

        [DocgenIgnore]
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
