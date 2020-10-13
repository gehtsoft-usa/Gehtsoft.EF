using Hime.Redist;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlConstantCollection : IReadOnlyList<SqlConstant>
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
    }
}
