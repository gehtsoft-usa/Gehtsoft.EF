using Gehtsoft.EF.Entities;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlSortSpecification
    {
        internal SqlBaseExpression Expression { get; } = null;
        internal SortDir Ordering { get; } = SortDir.Asc;

        internal SqlSortSpecification(SqlStatement parentStatement, SqlParser.SortSpecificationContext sortSpecificationNode, string source)
        {
            Expression = SqlExpressionParser.ParseExpression(parentStatement, sortSpecificationNode.expr(), source);
            if (sortSpecificationNode.orderingSpecification() != null)
            {
                Ordering = sortSpecificationNode.orderingSpecification().GetText() == "DESC" ? SortDir.Desc : SortDir.Asc;
            }
        }

        internal SqlSortSpecification(SqlBaseExpression expr, SortDir ordering = SortDir.Asc)
        {
            Expression = expr;
            Ordering = ordering;
        }
    }

    [Serializable]
    internal class SqlSortSpecificationCollection : IReadOnlyList<SqlSortSpecification>
    {
        private readonly List<SqlSortSpecification> mList = new List<SqlSortSpecification>();

        internal SqlSortSpecificationCollection()
        {
        }

        public SqlSortSpecification this[int index] => ((IReadOnlyList<SqlSortSpecification>)mList)[index];

        public int Count => ((IReadOnlyCollection<SqlSortSpecification>)mList).Count;

        public IEnumerator<SqlSortSpecification> GetEnumerator()
        {
            return ((IEnumerable<SqlSortSpecification>)mList).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mList).GetEnumerator();
        }

        internal void Add(SqlSortSpecification fieldAlias)
        {
            mList.Add(fieldAlias);
        }
    }
}
