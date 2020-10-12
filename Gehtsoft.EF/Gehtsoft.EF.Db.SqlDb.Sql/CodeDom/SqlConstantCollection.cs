using Hime.Redist;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlConstantCollection : IReadOnlyList<SqlConstant>, IEquatable<SqlConstantCollection>
    {
        private readonly List<SqlConstant> mList = new List<SqlConstant>();

        internal SqlConstantCollection()
        {

        }

        public SqlConstant this[int index] => ((IReadOnlyList<SqlConstant>)mList)[index];

        public int Count => ((IReadOnlyCollection<SqlConstant>)mList).Count;

        public IEnumerator<SqlConstant> GetEnumerator()
        {
            return ((IEnumerable<SqlConstant>)mList).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mList).GetEnumerator();
        }

        internal void Add(SqlConstant fieldAlias)
        {
            mList.Add(fieldAlias);
        }

        bool IEquatable<SqlConstantCollection>.Equals(SqlConstantCollection other) => Equals(other);
        internal virtual bool Equals(SqlConstantCollection other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            if (this.Count != other.Count)
                return false;
            for (int i = 0; i < Count; i++)
            {
                if (!this[i].Equals(other[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is SqlConstantCollection item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

    }
}
