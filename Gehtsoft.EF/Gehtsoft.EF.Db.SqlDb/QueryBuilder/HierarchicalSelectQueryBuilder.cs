using System.Data;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public abstract class HierarchicalSelectQueryBuilder : AQueryBuilder
    {
        protected TableDescriptor.ColumnInfo mReferenceColumn;
        protected TableDescriptor.ColumnInfo mPrimaryKey;
        protected TableDescriptor mDescriptor;

        protected string mRootParameterName;

        private static int gAlias = 0;
        private static readonly object gAliasMutex = new object();

        protected static string NextAlias
        {
            get
            {
                lock (gAliasMutex)
                {
                    string rc = $"hier{gAlias}";
                    gAlias = (gAlias + 1) & 0xffff; //0...65536
                    return rc;
                }
            }
        }

        protected string mQuery;

        public override string Query
        {
            get { return mQuery; }
        }

        protected HierarchicalSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table, TableDescriptor.ColumnInfo parentReferenceColumn, string rootParameterName) : base(specifics)
        {
            mReferenceColumn = parentReferenceColumn;
            mPrimaryKey = table.PrimaryKey;
            mRootParameterName = rootParameterName;
            mDescriptor = table;
        }

        protected TableDescriptor mQueryTableDescriptor;

        public bool IdOnlyMode { get; set; } = false;

        protected TableDescriptor GetTableDescriptor()
        {
            if (mQueryTableDescriptor == null)
            {
                if (Query == null)
                    PrepareQuery();
                mQueryTableDescriptor = new TableDescriptor($"({Query})")
                {
                    new TableDescriptor.ColumnInfo() { Name = "id", DbType = mPrimaryKey.DbType }
                };
                if (!IdOnlyMode)
                {
                    mQueryTableDescriptor.Add(new TableDescriptor.ColumnInfo() { Name = "parent", DbType = mReferenceColumn.DbType });
                    mQueryTableDescriptor.Add(new TableDescriptor.ColumnInfo() { Name = "level", DbType = DbType.Int32 });
                }
            }
            return mQueryTableDescriptor;
        }

        public TableDescriptor QueryTableDescriptor => GetTableDescriptor();
    }
}
