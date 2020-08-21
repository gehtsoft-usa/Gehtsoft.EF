using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public abstract class SqlBaseExpression : IEquatable<SqlBaseExpression>
    {
        /// <summary>
        /// The types of the Expression
        /// </summary>
        public enum ExpressionTypes
        {
            Binary,
            Unar,
            Field,
            Constant,
            Call,
            AggrFuncCall
        };

        /// <summary>
        /// The types of the output
        /// </summary>
        public enum ResultTypes
        {
            Boolean,
            Date,
            Integer,
            Double,
            String,
            Unknown
        };

        public Type RealType
        {
            get
            {
                Type retval = null;
                switch (ResultType)
                {
                    case ResultTypes.Integer:
                        retval = typeof(int);
                        break;
                    case ResultTypes.Boolean:
                        retval = typeof(bool);
                        break;
                    case ResultTypes.Date:
                        retval = typeof(DateTime);
                        break;
                    case ResultTypes.Double:
                        retval = typeof(double);
                        break;
                    case ResultTypes.String:
                        retval = typeof(string);
                        break;
                }
                return retval;
            }
        }

        public static ResultTypes GetResultType(Type type)
        {
            if (type == typeof(int))
                return ResultTypes.Integer;
            if (type == typeof(bool))
                return ResultTypes.Boolean;
            if (type == typeof(double))
                return ResultTypes.Double;
            if (type == typeof(DateTime))
                return ResultTypes.Date;
            if (type == typeof(string))
                return ResultTypes.String;
            return ResultTypes.Unknown;
        }

        public abstract ExpressionTypes ExpressionType { get; }

        public abstract ResultTypes ResultType { get; }

        public virtual bool Equals(SqlBaseExpression other)
        {
            if (other == null)
                return false;
            return (this.ExpressionType == other.ExpressionType && this.ResultType == other.ResultType);
        }

        public override bool Equals(object obj)
        {
            if (obj is SqlBaseExpression item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    /// <summary>
    /// A collection of fields with possible alias
    /// </summary>
    [Serializable]
    public class SqlBaseExpressionCollection : IReadOnlyList<SqlBaseExpression>, IEquatable<SqlBaseExpressionCollection>
    {
        private readonly List<SqlBaseExpression> mList = new List<SqlBaseExpression>();

        internal SqlBaseExpressionCollection()
        {

        }

        /// <summary>
        /// Returns the expression by its index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public SqlBaseExpression this[int index] => ((IReadOnlyList<SqlBaseExpression>)mList)[index];

        /// <summary>
        /// Returns the number of expressions
        /// </summary>
        public int Count => ((IReadOnlyCollection<SqlBaseExpression>)mList).Count;

        public IEnumerator<SqlBaseExpression> GetEnumerator()
        {
            return ((IEnumerable<SqlBaseExpression>)mList).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mList).GetEnumerator();
        }

        internal void Add(SqlBaseExpression expression) => mList.Add(expression);

        public virtual bool Equals(SqlBaseExpressionCollection other)
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
            if (obj is SqlBaseExpressionCollection item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
