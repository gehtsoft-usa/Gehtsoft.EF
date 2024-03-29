﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Hime.Redist;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class BreakStatement : Statement
    {
        internal BreakStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : this(builder, currentSource, statementNode.Position.Line, statementNode.Position.Column)
        {
        }

        internal BreakStatement(SqlCodeDomBuilder builder, string currentSource = null, int line = 0, int column = 0)
            : base(builder, StatementType.Break)
        {
            bool found = false;
            if (builder.BlockDescriptors.Count > 0)
            {
                BlockDescriptor[] array = builder.BlockDescriptors.ToArray();
                for (int i = array.Length - 1; i >= 0; i--)
                {
                    BlockDescriptor descr = array[i];
                    if (descr.StatementType == StatementType.Loop || descr.StatementType == StatementType.Switch)
                    {
                        found = true;
                        break;
                    }
                }
            }
            if (!found)
            {
                throw new SqlParserException(new SqlError(currentSource, line, column, "Unexpected operator BREAK (out of LOOP)"));
            }
        }

        internal BreakStatement()
            : base(null, StatementType.Break)
        {
        }

        internal override Expression ToLinqWxpression()
        {
            BlockDescriptor[] array = CodeDomBuilder.BlockDescriptors.ToArray();
            BlockDescriptor foundDescr = null;
            for (int i = array.Length - 1; i >= 0; i--)
            {
                BlockDescriptor descr = array[i];
                if (descr.StatementType == StatementType.Loop || descr.StatementType == StatementType.Switch)
                {
                    foundDescr = descr;
                    break;
                }
            }
            if (foundDescr == null)
            {
                throw new SqlParserException(new SqlError(null, 0, 0, "Runtime error: BREAK out of appropriate body"));
            }
            return Expression.Block(
                Expression.Call(Expression.Constant(CodeDomBuilder), "BreakRun", null),
                Expression.Goto(foundDescr.EndLabel)
            );
        }
    }
}
