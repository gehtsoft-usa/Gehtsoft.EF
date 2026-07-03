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
    internal class CloseCursorStatement : Statement
    {
        internal GlobalParameter CursorParameter { get; }
        internal CloseCursorStatement(SqlCodeDomBuilder builder, SqlParser.CloseCursorStatementContext statementNode, string currentSource)
            : base(builder, StatementType.CloseCursor)
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

        internal CloseCursorStatement(SqlCodeDomBuilder builder, GlobalParameter cursorParameter)
            : base(builder, StatementType.CloseCursor)
        {
            CursorParameter = cursorParameter;
        }

        internal void Run()
        {
            string globalVariableName = CursorParameter.Name;
            Tuple<SqlSelectStatement, SelectRunner> pair = (Tuple<SqlSelectStatement, SelectRunner>)CodeDomBuilder.FindGlobalParameter(globalVariableName).Value;
            if (pair == null)
                throw new SqlParserException(new SqlError(null, 0, 0, "Possibly cursor is not opened"));
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
