using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hime.Redist;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class BreakStatement : Statement
    {
        internal BreakStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : this(builder, currentSource, statementNode.Position.Line, statementNode.Position.Column)
        {
        }

        internal BreakStatement(SqlCodeDomBuilder builder, string currentSource = null, int line = 0, int column = 0)
            : base(builder, StatementType.Break)
        {
            IStatementSetEnvironment current = builder.TopEnvironment;
            bool found = false;
            while (current != null)
            {
                if (current.ParentStatement != null && current.ParentStatement.Type == StatementType.Loop)
                {
                    found = true;
                    break;
                }
                current = current.ParentEnvironment;
            }
            if (!found)
            {
                throw new SqlParserException(new SqlError(currentSource, line, column, $"Unexpected operator BREAK (out of LOOP)"));
            }
        }

        public virtual bool Equals(BreakStatement other)
        {
            if (other is BreakStatement stmt)
            {
                return true;
            }
            return base.Equals(other);
        }

        public override bool Equals(Statement obj)
        {
            if (obj is BreakStatement item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}
