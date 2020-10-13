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
    internal class BlockStatement : Statement
    {
        protected Expression LinqExpression { get; set; }

        internal BlockStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.Block)
        {
            ASTNode node = statementNode.Children[0];
            LinqExpression = builder.ParseNodeToLinq("Block Body", node, this);
        }
        protected BlockStatement(SqlCodeDomBuilder builder, StatementType type)
            : base(builder, type)
        {
        }


        internal override Expression ToLinqWxpression()
        {
            return LinqExpression;
        }
    }

    internal class DummyPersistBlock : Statement
    {
        internal DummyPersistBlock(SqlCodeDomBuilder builder)
            : base(builder, StatementType.DummyPersist)
        {
        }


        internal override Expression ToLinqWxpression()
        {
            return null;
        }
    }

}
