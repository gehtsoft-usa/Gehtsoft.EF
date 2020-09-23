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
    public class DeclareCursorStatement : Statement
    {
        public string Name { get; }
        public SqlSelectStatement SelectStatement { get; }
        internal DeclareCursorStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.DeclareCursor)
        {
            Name = $"?{statementNode.Children[0].Value}";
            ResultTypes resultType = ResultTypes.Cursor;

            if (!builder.AddGlobalParameter(Name, resultType, true))
            {
                throw new SqlParserException(new SqlError(currentSource,
                    statementNode.Children[0].Position.Line,
                    statementNode.Children[0].Position.Column,
                    $"Duplicate declared name ({Name})"));
            }

            SelectStatement = new SqlSelectStatement(this.CodeDomBuilder, statementNode.Children[1], currentSource);
        }

        internal DeclareCursorStatement(SqlCodeDomBuilder builder, string name, SqlSelectStatement selectStatement)
            : base(builder, StatementType.DeclareCursor)
        {
            Name = name;
            SelectStatement = selectStatement;
        }

        public virtual bool Equals(DeclareCursorStatement other)
        {
            if (other is DeclareCursorStatement stmt)
            {
                return Name == stmt.Name && SelectStatement.Equals(stmt.SelectStatement);
            }
            return base.Equals(other);
        }

        public override bool Equals(Statement obj)
        {
            if (obj is DeclareCursorStatement item)
                return Equals(item);
            return base.Equals(obj);
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
