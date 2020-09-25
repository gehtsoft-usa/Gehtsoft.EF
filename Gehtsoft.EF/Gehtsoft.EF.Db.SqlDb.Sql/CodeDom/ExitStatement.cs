using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Hime.Redist;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class ExitStatement : Statement
    {
        public SqlBaseExpression ExitExpression { get; } = null;

        internal ExitStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.Exit)
        {
            if (statementNode.Children.Count > 0)
            {
                ASTNode expressionNode = statementNode.Children[0];
                ExitExpression = SqlExpressionParser.ParseExpression(this, expressionNode, currentSource);
                if (!Statement.IsCalculable(ExitExpression))
                {
                    throw new SqlParserException(new SqlError(currentSource,
                        expressionNode.Position.Line,
                        expressionNode.Position.Column,
                        $"Not calculable expression in EXIT statement"));
                }
                if(ExitExpression.ResultType == SqlBaseExpression.ResultTypes.Cursor)
                {
                    throw new SqlParserException(new SqlError(currentSource,
                        expressionNode.Position.Line,
                        expressionNode.Position.Column,
                        $"Cursor expression can not be used in EXIT statement"));
                }
            }
        }

        internal ExitStatement(SqlCodeDomBuilder builder, SqlBaseExpression exitExpression)
            : base(builder, StatementType.Exit)
        {
            ExitExpression = exitExpression;
            if (!Statement.IsCalculable(ExitExpression))
            {
                throw new SqlParserException(new SqlError(null, 0, 0,
                    $"Not calculable expression in EXIT statement"));
            }
            if (ExitExpression.ResultType == SqlBaseExpression.ResultTypes.Cursor)
            {
                throw new SqlParserException(new SqlError(null, 0, 0,
                    $"Cursor expression can not be used in EXIT statement"));
            }
        }

        internal override Expression ToLinqWxpression()
        {
            BlockDescriptor descr =  CodeDomBuilder.BlockDescriptors.ToArray()[0];
            return Expression.Block(
                Expression.Call(Expression.Constant(CodeDomBuilder), "ExitRun", null, Expression.Constant(this.ExitExpression)),
                Expression.Goto(descr.EndLabel)
            );
        }

        public virtual bool Equals(ExitStatement other)
        {
            if (other is ExitStatement stmt)
            {
                if (ExitExpression == null && stmt.ExitExpression != null)
                    return false;
                if (ExitExpression != null && !ExitExpression.Equals(stmt.ExitExpression))
                    return false;
                return true;
            }
            return base.Equals(other);
        }

        public override bool Equals(Statement obj)
        {
            if (obj is ExitStatement item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}
