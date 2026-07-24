using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
#pragma warning disable S1694 // abstract base owns the nested TableType taxonomy and is the AST-node base; keep as a class
    internal abstract class SqlTableSpecification
    {
        /// <summary>
        /// The types of the table
        /// </summary>
        internal enum TableType
        {
            Primary,
            QualifiedJoin,
            AutoJoin
        };

        internal abstract TableType Type { get; }
    }
#pragma warning restore S1694

    /// <summary>
    /// A collection of tables
    /// </summary>
    [Serializable]
    internal class SqlTableSpecificationCollection : IReadOnlyList<SqlTableSpecification>
    {
        private readonly List<SqlTableSpecification> mList = new List<SqlTableSpecification>();

        internal SqlTableSpecificationCollection()
        {
        }

        /// <summary>
        /// Returns the table by its index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public SqlTableSpecification this[int index] => ((IReadOnlyList<SqlTableSpecification>)mList)[index];

        /// <summary>
        /// Returns the number of tables
        /// </summary>
        public int Count => ((IReadOnlyCollection<SqlTableSpecification>)mList).Count;

        public IEnumerator<SqlTableSpecification> GetEnumerator()
        {
            return ((IEnumerable<SqlTableSpecification>)mList).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mList).GetEnumerator();
        }

        internal void Add(SqlTableSpecification table) => mList.Add(table);
    }
}
