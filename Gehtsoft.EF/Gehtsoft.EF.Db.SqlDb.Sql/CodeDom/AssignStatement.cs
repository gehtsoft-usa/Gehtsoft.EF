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
    internal class AssignStatement : Statement
    {
        internal AssignExpression AssignExpression { get; }
        internal AssignStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.Assign)
        {
            AssignExpression = new AssignExpression(this, statementNode.Children[0], statementNode.Children[1], currentSource);
        }

        internal AssignStatement(SqlCodeDomBuilder builder, AssignExpression assignExpression)
            : base(builder, StatementType.Assign)
        {
            AssignExpression = assignExpression;
        }

        internal void Run()
        {
            SqlConstant param = StatementRunner.CalculateExpression(AssignExpression.RightOperand, CodeDomBuilder, CodeDomBuilder.Connection);
            CodeDomBuilder.UpdateGlobalParameter(AssignExpression.LeftOperand.Name, param);
        }

        internal override Expression ToLinqWxpression()
        {
            return Expression.Call(Expression.Constant(this), "Run", null);
        }
    }
}
