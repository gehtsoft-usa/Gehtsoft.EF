using Hime.Redist;
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

        internal SqlUpdateAssign(SqlStatement parentStatement, ASTNode updateAssignNode, string source)
        {
            Field = new SqlField(parentStatement, updateAssignNode.Children[0], source);
            ASTNode operand = updateAssignNode.Children[1];

            if (operand.Symbol.ID == SqlParser.ID.VariableSelect)
            {
                Select = new SqlSelectStatement(parentStatement.CodeDomBuilder, operand, source);
                if (Select.SelectList.FieldAliasCollection.Count != 1)
                {
                    throw new SqlParserException(new SqlError(source,
                        operand.Position.Line,
                        operand.Position.Column,
                        $"Expected 1 column in inner SELECT {operand.Symbol.Name} ({operand.Value ?? "null"})"));
                }
            }
            else
            {
                Expression = SqlExpressionParser.ParseExpression(parentStatement, operand, source);
            }
        }
        internal SqlUpdateAssign(SqlField field, SqlBaseExpression expression)
        {
            Field = field;
            Expression = expression;
        }
        internal SqlUpdateAssign(SqlField field, SqlSelectStatement select)
        {
            Field = field;
            Select = select;
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
