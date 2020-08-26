using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public abstract class SqlTableSpecification : IEquatable<SqlTableSpecification>
    {
        /// <summary>
        /// The types of the table
        /// </summary>
        public enum TableType
        {
            Primary,
            QualifiedJoin
        };

        public abstract TableType Type { get; }

        public virtual bool Equals(SqlTableSpecification other)
        {
            if (other == null)
                return false;
            return (this.Type == other.Type);
        }

        public override bool Equals(object obj)
        {
            if (obj is SqlTableSpecification item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }


    /// <summary>
    /// A collection of tables
    /// </summary>
    [Serializable]
    public class SqlTableSpecificationCollection : IReadOnlyList<SqlTableSpecification>
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
