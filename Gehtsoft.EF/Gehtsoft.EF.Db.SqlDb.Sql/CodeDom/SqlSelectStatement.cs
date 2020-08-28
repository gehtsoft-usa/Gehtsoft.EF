﻿using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    /// <summary>
    /// Select statement
    /// </summary>
    public class SqlSelectStatement : SqlStatement
    {
        public string SetQuantifier { get; } = string.Empty;
        public SqlSelectList SelectList { get; } = null;
        public SqlFromClause FromClause { get; } = null;
        public SqlWhereClause WhereClause { get; } = null;
        public int Offset { get; internal set; } = 0;
        public int Limit { get; internal set; } = 0;

        internal SqlSelectStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementId.Select, currentSource, statementNode.Position.Line, statementNode.Position.Column)
        {
            int disp = 0;

            if (statementNode.Children[0].Symbol.ID == SqlParser.ID.VariableSetQuantifier)
            {
                SetQuantifier = statementNode.Children[0].Children[0].Value;
                disp++;
            }

            ASTNode selectListNode = statementNode.Children[disp];
            ASTNode tableExpressionNode = statementNode.Children[disp + 1];
            FromClause = new SqlFromClause(this, tableExpressionNode.Children[0], currentSource);
            SelectList = new SqlSelectList(this, selectListNode, currentSource);
            if (tableExpressionNode.Children.Count > 1)
            {
                ASTNode whereNode = tableExpressionNode.Children[1];
                WhereClause = new SqlWhereClause(this, whereNode, currentSource);
                if (WhereClause.RootExpression.ResultType != SqlBaseExpression.ResultTypes.Boolean)
                {
                    throw new SqlParserException(new SqlError(currentSource,
                        whereNode.Position.Line,
                        whereNode.Position.Column,
                        $"Result of WHERE should be boolean {whereNode.Symbol.Name} ({whereNode.Value ?? "null"})"));
                }
                if (HasAggregateFunctions(WhereClause.RootExpression))
                {
                    throw new SqlParserException(new SqlError(currentSource,
                        whereNode.Position.Line,
                        whereNode.Position.Column,
                        $"WHERE expression should not contain calls of aggregate functions ({whereNode.Value ?? "null"})"));
                }
            }

            if(statementNode.Children.Count > disp + 2)
            {
                int nextsCount = statementNode.Children.Count - (disp + 2);
                for (int i = 0; i < nextsCount; i++)
                {
                    ASTNode nextNode = statementNode.Children[disp + 2 + i];
                    if (nextNode.Symbol.ID == SqlParser.ID.VariableLimitOffset)
                    {
                        foreach (ASTNode node in nextNode.Children)
                        {
                            if (node.Symbol.ID == SqlParser.ID.VariableOffset)
                            {
                                Offset = int.Parse(node.Children[0].Value);
                            }
                            if (node.Symbol.ID == SqlParser.ID.VariableLimit)
                            {
                                Limit = int.Parse(node.Children[0].Value);
                            }
                        }
                    }
                }
            }
        }

        internal SqlSelectStatement(SqlCodeDomBuilder builder, SqlSelectList selectList, SqlFromClause fromClause, SqlWhereClause whereClause = null)
            : base(builder, StatementId.Select, null, 0, 0)
        {
            SetQuantifier = string.Empty;
            FromClause = fromClause;
            SelectList = selectList;
            WhereClause = whereClause;
        }

        internal SqlSelectStatement(SqlCodeDomBuilder builder, string setQuantifier, SqlSelectList selectList, SqlFromClause fromClause, SqlWhereClause whereClause = null)
            : base(builder, StatementId.Select, null, 0, 0)
        {
            SetQuantifier = setQuantifier;
            FromClause = fromClause;
            SelectList = selectList;
            WhereClause = whereClause;
        }

        public virtual bool Equals(SqlSelectStatement other)
        {
            if (other is SqlSelectStatement stmt)
            {
                return SelectList.Equals(stmt.SelectList) &&
                       SetQuantifier.Equals(stmt.SetQuantifier) &&
                       FromClause.Equals(stmt.FromClause) &&
                       Limit == stmt.Limit &&
                       Offset == stmt.Offset &&
                       (WhereClause == null && stmt.WhereClause == null ||
                        WhereClause != null && WhereClause.Equals(stmt.WhereClause));
            }
            return base.Equals(other);
        }

        public override bool Equals(SqlStatement obj)
        {
            if (obj is SqlSelectStatement item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}
