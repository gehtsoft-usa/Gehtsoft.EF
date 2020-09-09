using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    /// <summary>
    /// Collection of statements
    /// </summary>
    public class StatementCollection : IReadOnlyList<Statement>, IEquatable<StatementCollection>
    {
        private readonly List<Statement> mCollection = new List<Statement>();

        public Statement this[int index] => ((IReadOnlyList<Statement>)mCollection)[index];

        public int Count => ((IReadOnlyCollection<Statement>)mCollection).Count;

        public IEnumerator<Statement> GetEnumerator()
        {
            return ((IEnumerable<Statement>)mCollection).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mCollection).GetEnumerator();
        }

        internal StatementCollection()
        {

        }

        internal void Add(Statement statement) => mCollection.Add(statement);

        public virtual bool Equals(StatementCollection other)
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
            if (obj is StatementCollection item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
