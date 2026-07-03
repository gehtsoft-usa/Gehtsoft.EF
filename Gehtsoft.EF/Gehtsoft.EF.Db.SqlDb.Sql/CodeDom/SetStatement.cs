using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SetStatement : Statement
    {
        internal SetItemCollection SetItems { get; }

        internal SetStatement(SqlCodeDomBuilder builder, SqlParser.SetStatementContext statementNode, string currentSource)
            : base(builder, StatementType.Set)
        {
            SetItems = new SetItemCollection();
            foreach (SqlParser.SetItemContext node in statementNode.setList().setItem())
            {
                string name = node.IDENTIFIER().GetText();
                SqlBaseExpression existing = builder.FindGlobalParameter($"?{name}");
                if (node.expr() != null)
                {
                    SqlParser.ExprContext expressionNode = node.expr();
                    SqlBaseExpression expression = SqlExpressionParser.ParseExpression(this, expressionNode, currentSource);
                    if (!Statement.IsCalculable(expression))
                    {
                        throw new SqlParserException(new SqlError(currentSource,
                            expressionNode.Line(),
                            expressionNode.Col(),
                            $"Not calculable expression in SET statement ({expressionNode.GetText()})"));
                    }
                    if (existing != null)
                    {
                        if (existing.ResultType != expression.ResultType)
                        {
                            throw new SqlParserException(new SqlError(currentSource,
                                expressionNode.Line(),
                                expressionNode.Col(),
                                $"Expression in SET statement doesn't match type of declared before ({existing.ResultType})"));
                        }
                    }
                    else
                    {
                        builder.AddGlobalParameter($"?{name}", expression.ResultType);
                    }
                    SetItems.Add(new SetItem(name, expression));
                }
                else
                {
                    SetItems.Add(new SetItem(name));
                }
            }
        }

        internal SetStatement(SqlCodeDomBuilder builder, SetItemCollection setItems)
            : base(builder, StatementType.Set)
        {
            SetItems = setItems;
        }

        internal void Run()
        {
            foreach (SetItem item in this.SetItems)
            {
                string name = item.Name;
                SqlBaseExpression sourceExpression = item.Expression;
                SqlConstant resultConstant = StatementRunner.CalculateExpression(sourceExpression, CodeDomBuilder, CodeDomBuilder.Connection);
                if (resultConstant == null)
                {
                    throw new SqlParserException(new SqlError(null, 0, 0, "Runtime error while SET execution"));
                }
                CodeDomBuilder.UpdateGlobalParameter($"?{name}", resultConstant);
            }
        }

        internal override Expression ToLinqWxpression()
        {
            return Expression.Call(Expression.Constant(this), "Run", null);
        }
    }

    internal class SetItem
    {
        internal string Name { get; }
        internal SqlBaseExpression Expression { get; }

        internal SetItem(string name, SqlBaseExpression expression)
        {
            Name = name;
            Expression = expression;
        }

        internal SetItem(string name)
        {
            Name = name;
            Expression = new SqlConstant(null, SqlBaseExpression.ResultTypes.Unknown);
        }
    }

    internal class SetItemCollection : IReadOnlyList<SetItem>
    {
        private readonly List<SetItem> mList = new List<SetItem>();

        internal SetItemCollection()
        {
        }

        internal SetItem FindByName(string name) => mList.SingleOrDefault(t => t.Name == name);

        public SetItem this[int index] => ((IReadOnlyList<SetItem>)mList)[index];

        public int Count => ((IReadOnlyCollection<SetItem>)mList).Count;

        public IEnumerator<SetItem> GetEnumerator()
        {
            return ((IEnumerable<SetItem>)mList).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mList).GetEnumerator();
        }

        internal void Add(SetItem fieldName)
        {
            mList.Add(fieldName);
        }
    }
}
