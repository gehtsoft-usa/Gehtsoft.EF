using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Hime.Redist;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal  class CloseCursorStatement : Statement
    {
        internal  GlobalParameter CursorParameter { get; }
        internal CloseCursorStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.CloseCursor)
        {
            ASTNode expressionNode;
            expressionNode = statementNode.Children[0];
            SqlBaseExpression cursorExpression = SqlExpressionParser.ParseExpression(this, expressionNode, currentSource);
            if (cursorExpression.ResultType != ResultTypes.Cursor)
            {
                throw new SqlParserException(new SqlError(currentSource,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    $"Parameter of OPEN CURSOR is not declared as CURSOR"));
            }
            if (cursorExpression.ExpressionType != ExpressionTypes.GlobalParameter)
            {
                throw new SqlParserException(new SqlError(currentSource,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    $"Parameter of OPEN CURSOR should be global variable"));
            }
            CursorParameter = (GlobalParameter)cursorExpression;
        }

        internal CloseCursorStatement(SqlCodeDomBuilder builder, GlobalParameter cursorParameter)
            : base(builder, StatementType.CloseCursor)
        {
            CursorParameter = cursorParameter;
        }

        internal virtual bool Equals(CloseCursorStatement other)
        {
            if (other is CloseCursorStatement stmt)
            {
                return CursorParameter.Equals(stmt.CursorParameter);
            }
            return base.Equals(other);
        }

        internal override bool Equals(Statement obj)
        {
            if (obj is CloseCursorStatement item)
                return Equals(item);
            return base.Equals(obj);
        }

        internal void Run()
        {
            string globalVariableName = CursorParameter.Name;
            Tuple<SqlSelectStatement, SelectRunner> pair = (Tuple<SqlSelectStatement, SelectRunner>)CodeDomBuilder.FindGlobalParameter(globalVariableName).Value;
            if (pair == null)
                throw new SqlParserException(new SqlError(null, 0, 0, $"Possibly cursor is not opened"));
            SqlSelectStatement selectStatement = pair.Item1;
            SelectRunner selectRunner = pair.Item2;
            selectRunner.Close();

            CodeDomBuilder.UpdateGlobalParameter(globalVariableName,
                new SqlConstant(selectStatement, ResultTypes.Cursor));
        }

        internal override Expression ToLinqWxpression()
        {
            return Expression.Call(Expression.Constant(this), "Run", null);
        }
    }
}
