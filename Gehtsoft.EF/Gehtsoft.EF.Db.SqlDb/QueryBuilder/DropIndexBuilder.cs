using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public class DropIndexBuilder : AQueryBuilder
    {
        protected readonly string mTable;
        protected readonly string mName;
        protected string mQuery;
        override public string Query => mQuery;

        public DropIndexBuilder(SqlDbLanguageSpecifics specifics, string table, string name) : base(specifics)
        {
            mSpecifics = specifics;
            mTable = table;
            mName = name;
        }

        public override void PrepareQuery()
        {
            if (mQuery != null)
                return;

            StringBuilder builder = new StringBuilder();
            builder.Append(mSpecifics.PreBlock);
            builder.Append(mSpecifics.PreQueryInBlock);

            builder.Append("DROP INDEX IF EXISTS ")
                .Append(mTable)
                .Append('_')
                .Append(mName)
                .Append(" ON ")
                .Append(mTable);

            if (mSpecifics.TerminateWithSemicolon)
                builder.Append(';');

            builder.Append(mSpecifics.PostQueryInBlock);
            builder.Append(mSpecifics.PostBlock);

            mQuery = builder.ToString();
        }
    }
}