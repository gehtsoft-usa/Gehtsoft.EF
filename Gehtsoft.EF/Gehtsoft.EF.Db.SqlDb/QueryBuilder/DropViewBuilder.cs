using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// The query builder for `DROP VIEW` command.
    ///
    /// Use <see cref="SqlDbConnection.GetDropViewBuilder(string)"/> to create an instance of this object.
    /// </summary>
    public class DropViewBuilder : AQueryBuilder
    {
        protected readonly string mName;
        protected string mQuery;
        override public string Query => mQuery;

        public DropViewBuilder(SqlDbLanguageSpecifics specifics, string name) : base(specifics)
        {
            mSpecifics = specifics;
            mName = name;
        }

        public override void PrepareQuery()
        {
            if (mQuery != null)
                return;

            StringBuilder builder = new StringBuilder();
            builder.Append(mSpecifics.PreBlock);
            builder.Append(mSpecifics.PreQueryInBlock);

            builder.Append("DROP VIEW IF EXISTS ").Append(mName);

            if (mSpecifics.TerminateWithSemicolon)
                builder.Append(';');

            builder.Append(mSpecifics.PostQueryInBlock);
            builder.Append(mSpecifics.PostBlock);

            mQuery = builder.ToString();
        }
    }
}