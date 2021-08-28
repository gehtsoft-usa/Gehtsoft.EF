using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// The query builder for `DROP TABLE` command.
    ///
    /// Use <see cref="SqlDbConnection.GetDropTableBuilder(TableDescriptor)"/> to create an instance of this object.
    /// </summary>
    public class DropTableBuilder : AQueryBuilder
    {
        protected string mQuery;
        protected TableDescriptor mDescriptor;

        [DocgenIgnore]
        public override string Query
        {
            get { return mQuery; }
        }

        [DocgenIgnore]
        internal protected DropTableBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor tableDescriptor) : base(specifics)
        {
            mQuery = null;
            mDescriptor = tableDescriptor;
        }

        [DocgenIgnore]
        public override void PrepareQuery()
        {
            if (mQuery != null)
                return;
            mQuery = $"DROP TABLE IF EXISTS {mDescriptor.Name}";
        }
    }
}