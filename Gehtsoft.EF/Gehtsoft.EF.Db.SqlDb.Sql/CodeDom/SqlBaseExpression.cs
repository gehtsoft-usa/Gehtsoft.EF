using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal  abstract class SqlBaseExpression
    {
        /// <summary>
        /// The types of the Expression
        /// </summary>
        internal  enum ExpressionTypes
        {
            Binary,
            Unar,
            Field,
            Constant,
            Call,
            AggrFuncCall,
            In,
            SelectExpression,
            GlobalParameter,
            GetLastResult,
            GetRowsCount,
            GetRow,
            GetField,
            NewRowSet,
            NewRow,
            Fetch,
            Assign
        };

        /// <summary>
        /// The types of the output
        /// </summary>
        internal enum ResultTypes
        {
            Boolean,
            DateTime,
            Integer,
            Double,
            String,
            Row,
            RowSet,
            Cursor,
            Unknown
        };

        static internal Type GetSystemType(ResultTypes type)
        {
            Type retval = null;
            switch (type)
            {
                case ResultTypes.Integer:
                    retval = typeof(int);
                    break;
                case ResultTypes.Boolean:
                    retval = typeof(bool);
                    break;
                case ResultTypes.DateTime:
                    retval = typeof(DateTime);
                    break;
                case ResultTypes.Double:
                    retval = typeof(double);
                    break;
                case ResultTypes.String:
                    retval = typeof(string);
                    break;
                case ResultTypes.Row:
                    retval = typeof(Dictionary<string, object>);
                    break;
                case ResultTypes.RowSet:
                    retval = typeof(List<object>);
                    break;
                case ResultTypes.Cursor:
                    retval = typeof(SqlSelectStatement);
                    break;
            }
            return retval;
        }

        internal Type SystemType
        {
            get
            {
                return GetSystemType(ResultType);
            }
        }

        internal  static ResultTypes GetResultType(Type type)
        {
            if (type == typeof(int))
                return ResultTypes.Integer;
            if (type == typeof(bool))
                return ResultTypes.Boolean;
            if (type == typeof(double))
                return ResultTypes.Double;
            if (type == typeof(DateTime))
                return ResultTypes.DateTime;
            if (type == typeof(string))
                return ResultTypes.String;
            if (type == typeof(Dictionary<string, object>))
                return ResultTypes.Row;
            if (type == typeof(List<object>))
                return ResultTypes.RowSet;
            if (type == typeof(SqlSelectStatement))
                return ResultTypes.Cursor;
            return ResultTypes.Unknown;
        }

        internal  abstract ExpressionTypes ExpressionType { get; }

        internal  abstract ResultTypes ResultType { get; }
    }

    /// <summary>
    /// A collection of fields with possible alias
    /// </summary>
    [Serializable]
    internal  class SqlBaseExpressionCollection : IReadOnlyList<SqlBaseExpression>
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
    }
}
