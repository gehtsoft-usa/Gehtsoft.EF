using Hime.Redist;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    [Serializable]
    internal class SqlFieldCollection : IReadOnlyList<SqlField>
    {
        private readonly List<SqlField> mList = new List<SqlField>();

        internal SqlFieldCollection()
        {
        }

        internal SqlField FindByName(string name) => mList.SingleOrDefault(t => t.FieldName == name);

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
    }
}
