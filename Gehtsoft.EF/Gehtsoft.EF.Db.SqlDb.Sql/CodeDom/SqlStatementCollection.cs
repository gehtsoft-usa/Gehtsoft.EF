using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    /// <summary>
    /// Collection of Sql statements
    /// </summary>
    public class SqlStatementCollection : IReadOnlyList<SqlStatement>, IEquatable<SqlStatementCollection>
    {
        private readonly List<SqlStatement> mCollection = new List<SqlStatement>();

        public SqlStatement this[int index] => ((IReadOnlyList<SqlStatement>)mCollection)[index];

        public int Count => ((IReadOnlyCollection<SqlStatement>)mCollection).Count;

        public IEnumerator<SqlStatement> GetEnumerator()
        {
            return ((IEnumerable<SqlStatement>)mCollection).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mCollection).GetEnumerator();
        }

        internal SqlStatementCollection()
        {

        }

        internal void Add(SqlStatement statement) => mCollection.Add(statement);

        public virtual bool Equals(SqlStatementCollection other)
        {
            if (other == null)
                return false;
            if (Count != other.Count)
                return false;
            for (int i = 0; i < Count; i++)
                if (!this[i].Equals(other[i]))
                    return false;
            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is SqlStatementCollection item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
