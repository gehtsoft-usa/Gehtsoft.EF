using Hime.Redist;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    [Serializable]
    public class SqlFieldCollection : IReadOnlyList<SqlField>, IEquatable<SqlFieldCollection>
    {
        private readonly List<SqlField> mList = new List<SqlField>();

        internal SqlFieldCollection()
        {

        }

        public SqlField FindByName(string name) => mList.Where(t => t.FieldName == name).SingleOrDefault();

        public SqlField this[int index] => ((IReadOnlyList<SqlField>)mList)[index];

        public int Count => ((IReadOnlyCollection<SqlField>)mList).Count;

        public IEnumerator<SqlField> GetEnumerator()
        {
            return ((IEnumerable<SqlField>)mList).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mList).GetEnumerator();
        }

        internal void Add(SqlField fieldName)
        {
            mList.Add(fieldName);
        }

        public virtual bool Equals(SqlFieldCollection other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            if (this.Count != other.Count)
                return false;
            for(int i=0; i < Count; i++)
            {
                if(!this[i].Equals(other[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is SqlFieldCollection item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

    }
}
