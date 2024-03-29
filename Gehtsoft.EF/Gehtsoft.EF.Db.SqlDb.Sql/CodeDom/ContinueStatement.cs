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
    internal class ContinueStatement : Statement
    {
        internal ContinueStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : this(builder, currentSource, statementNode.Position.Line, statementNode.Position.Column)
        {
        }

        internal ContinueStatement(SqlCodeDomBuilder builder, string currentSource = null, int line = 0, int column = 0)
            : base(builder, StatementType.Continue)
        {
            BlockDescriptor[] array = CodeDomBuilder.BlockDescriptors.ToArray();
            BlockDescriptor foundDescr = null;
            for (int i = array.Length - 1; i >= 0; i--)
            {
                BlockDescriptor descr = array[i];
                if (descr.StatementType == StatementType.Loop)
                {
                    foundDescr = descr;
                    break;
                }
            }
            if (foundDescr == null)
            {
                throw new SqlParserException(new SqlError(currentSource, line, column, "Unexpected operator CONTINUE (out of LOOP)"));
            }
        }

        internal ContinueStatement()
            : base(null, StatementType.Continue)
        {
        }

        internal override Expression ToLinqWxpression()
        {
            BlockDescriptor[] array = CodeDomBuilder.BlockDescriptors.ToArray();
            BlockDescriptor foundDescr = null;
            for (int i = array.Length - 1; i >= 0; i--)
            {
                BlockDescriptor descr = array[i];
                if (descr.StatementType == StatementType.Loop)
                {
                    foundDescr = descr;
                    break;
                }
            }
            if (foundDescr == null)
            {
                throw new SqlParserException(new SqlError(null, 0, 0, "Runtime error: BREAK out of appropriate body"));
            }
            List<Expression> leaveSet = new List<Expression>();
            if (foundDescr.OnContinue != null)
                leaveSet.Add(foundDescr.OnContinue);
            leaveSet.Add(Expression.Call(Expression.Constant(CodeDomBuilder), "ContinueRun", null));
            leaveSet.Add(Expression.Goto(foundDescr.StartLabel));
            return Expression.Block(leaveSet);
        }
    }
}
