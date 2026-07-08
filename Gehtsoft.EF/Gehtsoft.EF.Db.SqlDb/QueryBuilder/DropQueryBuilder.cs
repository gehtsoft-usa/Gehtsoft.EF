using System.Collections.Generic;
using System.Text;
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
        protected readonly List<TableDescriptor> mDescriptors = new List<TableDescriptor>();

        [DocgenIgnore]
        public override string Query
        {
            get { return mQuery; }
        }

        [DocgenIgnore]
        internal protected DropTableBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor tableDescriptor) : base(specifics)
        {
            mQuery = null;
            mDescriptors.Add(tableDescriptor);
        }

        /// <summary>
        /// Adds another table to be dropped by the same query.
        ///
        /// The tables are dropped in the order they are added; add a table before the tables it
        /// depends on via a foreign key.
        /// </summary>
        /// <param name="tableDescriptor"></param>
        public void AddTable(TableDescriptor tableDescriptor)
        {
            mDescriptors.Add(tableDescriptor);
            mQuery = null;
        }

        [DocgenIgnore]
        public override void PrepareQuery()
        {
            if (mQuery != null)
                return;

            StringBuilder builder = new StringBuilder();
            builder.Append(mSpecifics.PreBlock);
            for (int i = 0; i < mDescriptors.Count; i++)
            {
                if (i > 0 && mSpecifics.TerminateWithSemicolon)
                    builder.Append(";\r\n");
                AppendDropTable(builder, mDescriptors[i]);
            }
            builder.Append(mSpecifics.PostBlock);
            mQuery = builder.ToString();
        }

        protected virtual void AppendDropTable(StringBuilder builder, TableDescriptor descriptor)
        {
            builder.Append("DROP TABLE IF EXISTS ").Append(descriptor.Name);
        }
    }
}
