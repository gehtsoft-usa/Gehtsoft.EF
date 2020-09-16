using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hime.Redist;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class BlockStatement : Statement
    {
        public StatementSetEnvironment Statements { get; protected set; }

        internal BlockStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.Block)
        {
            ASTNode node = statementNode.Children[0];
            Statements = builder.ParseNode("Block Body", node, this);
            Statements.ParentEnvironment = builder.TopEnvironment;
            builder.TopEnvironment = Statements;
        }

        internal BlockStatement(SqlCodeDomBuilder builder, StatementSetEnvironment statements)
            : base(builder, StatementType.Block)
        {
            Statements = statements;
            Statements.ParentEnvironment = builder.TopEnvironment;
            builder.TopEnvironment = Statements;
        }
        protected BlockStatement(SqlCodeDomBuilder builder, StatementType type)
            : base(builder, type)
        {
        }
        public virtual bool Equals(BlockStatement other)
        {
            if (other is BlockStatement stmt)
            {
                return Statements.Equals(stmt.Statements) && Type == stmt.Type;
            }
            return base.Equals(other);
        }

        public override bool Equals(Statement obj)
        {
            if (obj is BlockStatement item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}
