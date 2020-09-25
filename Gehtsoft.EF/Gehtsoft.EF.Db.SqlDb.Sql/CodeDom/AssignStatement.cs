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
    public class AssignStatement : Statement
    {
        public AssignExpression AssignExpression { get; }
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

        public virtual bool Equals(AssignStatement other)
        {
            if (other is AssignStatement stmt)
            {
                return AssignExpression.Equals(stmt.AssignExpression);
            }
            return base.Equals(other);
        }

        public override bool Equals(Statement obj)
        {
            if (obj is AssignStatement item)
                return Equals(item);
            return base.Equals(obj);
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
