using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class OpenCursorStatement : Statement
    {
        internal GlobalParameter CursorParameter { get; }
        internal OpenCursorStatement(SqlCodeDomBuilder builder, SqlParser.OpenCursorStatementContext statementNode, string currentSource)
            : base(builder, StatementType.OpenCursor)
        {
            SqlParser.GlobalParameterSimpleContext expressionNode = statementNode.globalParameterSimple();
            CursorParameter = new GlobalParameter(this, expressionNode);
            if (CursorParameter.ResultType != ResultTypes.Cursor)
            {
                throw new SqlParserException(new SqlError(currentSource,
                    expressionNode.Line(),
                    expressionNode.Col(),
                    "Parameter of OPEN CURSOR is not declared as CURSOR"));
            }
        }

        internal OpenCursorStatement(SqlCodeDomBuilder builder, GlobalParameter cursorParameter)
            : base(builder, StatementType.OpenCursor)
        {
            CursorParameter = cursorParameter;
        }

        internal void Run(SqlDbConnection connection)
        {
            string globalVariableName = CursorParameter.Name;
            SqlSelectStatement selectStatement = (SqlSelectStatement)CodeDomBuilder.FindGlobalParameter(globalVariableName).Value;
            if (selectStatement == null)
                throw new SqlParserException(new SqlError(null, 0, 0, "Possibly cursor is already opened"));

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
