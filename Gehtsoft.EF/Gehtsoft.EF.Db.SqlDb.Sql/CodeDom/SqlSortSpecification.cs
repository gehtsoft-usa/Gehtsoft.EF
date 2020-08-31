using Gehtsoft.EF.Entities;
using Hime.Redist;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class SqlSortSpecification : IEquatable<SqlSortSpecification>
    {

        public SqlBaseExpression Expression { get; } = null;
        public SortDir Ordering { get; } = SortDir.Asc;

        internal SqlSortSpecification(SqlStatement parentStatement, ASTNode sortSpecificationNode, string source)
        {
            Expression = SqlExpressionParser.ParseExpression(parentStatement, sortSpecificationNode.Children[0], source);
            if (sortSpecificationNode.Children.Count > 1)
            {
                Ordering = sortSpecificationNode.Children[1].Value == "DESC" ? SortDir.Desc : SortDir.Asc;
            }
        }

        internal SqlSortSpecification(SqlBaseExpression expr, SortDir ordering = SortDir.Asc)
        {
            Expression = expr;
            Ordering = ordering;
        }

        public virtual bool Equals(SqlSortSpecification other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;

            return Expression.Equals(other.Expression) && Ordering == other.Ordering;
        }

        public override bool Equals(object obj)
        {
            if (obj is SqlSortSpecification item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

    }

    [Serializable]
    public class SqlSortSpecificationCollection : IReadOnlyList<SqlSortSpecification>, IEquatable<SqlSortSpecificationCollection>
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

        public virtual bool Equals(SqlSortSpecificationCollection other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            if (this.Count != other.Count)
                return false;

            foreach (SqlSortSpecification thisFld in this)
            {
                foreach (SqlSortSpecification otherFld in other)
                {
                    if (!thisFld.Equals(otherFld))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is SqlSortSpecificationCollection item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

}
