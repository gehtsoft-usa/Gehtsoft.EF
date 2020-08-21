namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public class DropTableBuilder : AQueryBuilder
    {
        protected string mQuery;
        protected TableDescriptor mDescriptor;

        public override string Query
        {
            get { return mQuery; }
        }

        public DropTableBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor tableDescriptor) : base(specifics)
        {
            mQuery = null;
            mDescriptor = tableDescriptor;
        }

        public override void PrepareQuery()
        {
            if (mQuery != null)
                return;
            mQuery = $"DROP TABLE IF EXISTS {mDescriptor.Name}";
        }
    }
}