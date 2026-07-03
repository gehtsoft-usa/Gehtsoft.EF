using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlUpdateAssign
    {
        internal SqlField Field { get; } = null;
        internal SqlSelectStatement Select { get; } = null;
        internal SqlBaseExpression Expression { get; } = null;

        internal SqlUpdateAssign(SqlStatement parentStatement, SqlParser.UpdateAssignContext updateAssignNode, string source)
        {
            Field = new SqlField(parentStatement, updateAssignNode.field(), source);
            SqlParser.ExprContext operand = updateAssignNode.updateOperand().expr();

            // `field = (SELECT ...)` parses as a scalar-subquery expression (PrimaryExpr -> selectExpr);
            // preserve the original behavior of exposing it via the Select property.
            SqlParser.SelectExprContext selectExpr =
                (operand is SqlParser.PrimaryExprContext pe) ? pe.primary().selectExpr() : null;

            if (selectExpr != null)
            {
                SqlParser.SelectStatementContext selectNode = selectExpr.selectStatement();
                Select = new SqlSelectStatement(parentStatement.CodeDomBuilder, selectNode, source);
                if (Select.SelectList.FieldAliasCollection.Count != 1)
                {
                    throw new SqlParserException(new SqlError(source,
                        selectNode.Line(),
                        selectNode.Col(),
                        $"Expected 1 column in inner SELECT ({selectNode.GetText()})"));
                }
            }
            else
            {
                Expression = SqlExpressionParser.ParseExpression(parentStatement, operand, source);
            }
        }
    }

    [Serializable]
    internal class SqlUpdateAssignCollection : IReadOnlyList<SqlUpdateAssign>
    {
        private readonly List<SqlUpdateAssign> mList = new List<SqlUpdateAssign>();

        internal SqlUpdateAssignCollection()
        {
        }

        public SqlUpdateAssign this[int index] => ((IReadOnlyList<SqlUpdateAssign>)mList)[index];

        public int Count => ((IReadOnlyCollection<SqlUpdateAssign>)mList).Count;

        internal SqlUpdateAssign FindByFieldName(string name) => mList.SingleOrDefault(t => t.Field.FieldName == name);

        public IEnumerator<SqlUpdateAssign> GetEnumerator()
        {
            return ((IEnumerable<SqlUpdateAssign>)mList).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mList).GetEnumerator();
        }

        internal void Add(SqlUpdateAssign updateAssign)
        {
            mList.Add(updateAssign);
        }
    }
}
