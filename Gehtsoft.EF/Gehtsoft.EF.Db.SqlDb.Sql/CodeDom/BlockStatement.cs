﻿using System;
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
        internal StatementSetEnvironment Statements { get; set; }
        protected Expression LinqExpression { get; set; }

        internal BlockStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.Block)
        {
            ASTNode node = statementNode.Children[0];
            Statements = builder.ParseNode("Block Body", node, this);
            //Statements.ParentEnvironment = builder.TopEnvironment;
            //builder.TopEnvironment = Statements;
            if (builder.WhetherParseToLinq)
            {
                LinqExpression = builder.ParseNodeToLinq("Block Body", node, this);
            }
        }

        internal BlockStatement(SqlCodeDomBuilder builder, StatementSetEnvironment statements)
            : base(builder, StatementType.Block)
        {
            Statements = statements;
            //Statements.ParentEnvironment = builder.TopEnvironment;
            //builder.TopEnvironment = Statements;
        }
        protected BlockStatement(SqlCodeDomBuilder builder, StatementType type)
            : base(builder, type)
        {
        }


        internal override Expression ToLinqWxpression()
        {
            return LinqExpression;
        }

        internal virtual bool Equals(BlockStatement other)
        {
            if (other is BlockStatement stmt)
            {
                return Statements.Equals(stmt.Statements) && Type == stmt.Type;
            }
            return base.Equals(other);
        }

        internal override bool Equals(Statement obj)
        {
            if (obj is BlockStatement item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}
