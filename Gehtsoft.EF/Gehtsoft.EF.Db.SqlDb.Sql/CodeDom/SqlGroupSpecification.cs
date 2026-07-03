using Gehtsoft.EF.Entities;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlGroupSpecification
    {
        internal SqlBaseExpression Expression { get; } = null;

        internal SqlGroupSpecification(SqlStatement parentStatement, SqlParser.GroupSpecificationContext sortSpecificationNode, string source)
        {
            Expression = SqlExpressionParser.ParseExpression(parentStatement, sortSpecificationNode.expr(), source);
        }

        internal SqlGroupSpecification(SqlField field)
        {
            Expression = field;
        }
    }

    [Serializable]
    internal class SqlGroupSpecificationCollection : IReadOnlyList<SqlGroupSpecification>
    {
        private readonly List<SqlGroupSpecification> mList = new List<SqlGroupSpecification>();

        internal SqlGroupSpecificationCollection()
        {
        }

        public SqlGroupSpecification this[int index] => ((IReadOnlyList<SqlGroupSpecification>)mList)[index];

        public int Count => ((IReadOnlyCollection<SqlGroupSpecification>)mList).Count;

        public IEnumerator<SqlGroupSpecification> GetEnumerator()
        {
            return ((IEnumerable<SqlGroupSpecification>)mList).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mList).GetEnumerator();
        }

        internal void Add(SqlGroupSpecification fieldAlias)
        {
            mList.Add(fieldAlias);
        }
    }
}