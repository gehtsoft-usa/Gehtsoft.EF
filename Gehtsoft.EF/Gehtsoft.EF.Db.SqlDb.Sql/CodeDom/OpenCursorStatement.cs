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
    public class OpenCursorStatement : Statement
    {
        public GlobalParameter CursorParameter { get; }
        internal OpenCursorStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.OpenCursor)
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

        internal OpenCursorStatement(SqlCodeDomBuilder builder, GlobalParameter cursorParameter)
            : base(builder, StatementType.OpenCursor)
        {
            CursorParameter = cursorParameter;
        }

        public virtual bool Equals(OpenCursorStatement other)
        {
            if (other is OpenCursorStatement stmt)
            {
                return CursorParameter.Equals(stmt.CursorParameter);
            }
            return base.Equals(other);
        }

        public override bool Equals(Statement obj)
        {
            if (obj is OpenCursorStatement item)
                return Equals(item);
            return base.Equals(obj);
        }

        internal void Run(SqlDbConnection connection)
        {
            string globalVariableName = CursorParameter.Name;
            SqlSelectStatement selectStatement = (SqlSelectStatement)CodeDomBuilder.FindGlobalParameter(globalVariableName).Value;
            if (selectStatement == null)
                throw new SqlParserException(new SqlError(null, 0, 0, $"Possibly cursor is already opened"));

            SelectRunner selectRunner = new SelectRunner(CodeDomBuilder, connection);
            selectRunner.Open(selectStatement);

            CodeDomBuilder.UpdateGlobalParameter(globalVariableName,
                new SqlConstant(new Tuple<SqlSelectStatement, SelectRunner>(selectStatement, selectRunner), ResultTypes.Cursor));
        }

        internal override Expression ToLinqWxpression()
        {
            return Expression.Call(Expression.Constant(this), "Run", null, Expression.Constant(CodeDomBuilder.Connection));
        }
    }
}
