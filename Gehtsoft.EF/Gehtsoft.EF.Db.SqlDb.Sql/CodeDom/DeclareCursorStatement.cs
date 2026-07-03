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
    internal class DeclareCursorStatement : Statement
    {
        internal string Name { get; }
        internal SqlSelectStatement SelectStatement { get; }
        internal DeclareCursorStatement(SqlCodeDomBuilder builder, SqlParser.DeclareCursorStatementContext statementNode, string currentSource)
            : base(builder, StatementType.DeclareCursor)
        {
            Name = $"?{statementNode.IDENTIFIER().GetText()}";
            const ResultTypes resultType = ResultTypes.Cursor;

            if (!builder.AddGlobalParameter(Name, resultType))
            {
                throw new SqlParserException(new SqlError(currentSource,
                    statementNode.Line(),
                    statementNode.Col(),
                    $"Duplicate declared name ({Name})"));
            }

            SelectStatement = new SqlSelectStatement(this.CodeDomBuilder, statementNode.selectStatement(), currentSource);
        }

        internal DeclareCursorStatement(SqlCodeDomBuilder builder, string name, SqlSelectStatement selectStatement)
            : base(builder, StatementType.DeclareCursor)
        {
            Name = name;
            SelectStatement = selectStatement;
        }

        internal void Run()
        {
            CodeDomBuilder.UpdateGlobalParameter(Name, new SqlConstant(SelectStatement, ResultTypes.Cursor));
        }

        internal override Expression ToLinqWxpression()
        {
            return Expression.Call(Expression.Constant(this), "Run", null);
        }
    }
}
