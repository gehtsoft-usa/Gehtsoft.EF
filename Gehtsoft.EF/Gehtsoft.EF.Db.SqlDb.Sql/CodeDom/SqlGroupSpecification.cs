using Gehtsoft.EF.Entities;
using Hime.Redist;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlGroupSpecification : IEquatable<SqlGroupSpecification>
    {

        internal SqlBaseExpression Expression { get; } = null;

        internal SqlGroupSpecification(SqlStatement parentStatement, ASTNode sortSpecificationNode, string source)
        {
            Expression = SqlExpressionParser.ParseExpression(parentStatement, sortSpecificationNode.Children[0], source);

        }

        internal SqlGroupSpecification(SqlField field)
        {
            Expression = field;
        }

        bool IEquatable<SqlGroupSpecification>.Equals(SqlGroupSpecification other) => Equals(other);
        internal virtual bool Equals(SqlGroupSpecification other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;

            return Expression.Equals(other.Expression);
        }

        public override bool Equals(object obj)
        {
            if (obj is SqlGroupSpecification item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

    }

    [Serializable]
    internal class SqlGroupSpecificationCollection : IReadOnlyList<SqlGroupSpecification>, IEquatable<SqlGroupSpecificationCollection>
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

        bool IEquatable<SqlGroupSpecificationCollection>.Equals(SqlGroupSpecificationCollection other) => Equals(other);
        internal virtual bool Equals(SqlGroupSpecificationCollection other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            if (this.Count != other.Count)
                return false;

            foreach (SqlGroupSpecification thisFld in this)
            {
                foreach (SqlGroupSpecification otherFld in other)
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
            if (obj is SqlGroupSpecificationCollection item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

}