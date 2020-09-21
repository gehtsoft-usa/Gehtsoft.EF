using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    /// <summary>
    /// Delete statement
    /// </summary>
    public class SqlDeleteStatement : SqlStatement
    {
        public string TableName { get; } = null;
        public SqlWhereClause WhereClause { get; } = null;

        internal SqlDeleteStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementId.Delete, currentSource, statementNode.Position.Line, statementNode.Position.Column)
        {
            TableName = statementNode.Children[0].Value;
            try
            {
                this.AddEntityEntry(TableName, null);
            }
            catch
            {
                throw new SqlParserException(new SqlError(currentSource,
                    statementNode.Children[0].Position.Line,
                    statementNode.Children[0].Position.Column,
                    $"Not found entity with name '{TableName}'"));
            }

            if (statementNode.Children.Count > 1)
            {
                ASTNode whereNode = statementNode.Children[1];
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

        }

        internal SqlDeleteStatement(SqlCodeDomBuilder builder, string tableName, SqlWhereClause whereClause = null)
            : base(builder, StatementId.Delete, null, 0, 0)
        {
            TableName = tableName;
            try
            {
                this.AddEntityEntry(TableName, null);
            }
            catch
            {
                throw new SqlParserException(new SqlError(null, 0, 0, $"Not found entity with name '{TableName}'"));
            }
            WhereClause = whereClause;
        }

        internal override Expression ToLinqWxpression()
        {
            DeleteRunner runner = new DeleteRunner(CodeDomBuilder, CodeDomBuilder.Connection);
            return Expression.Call(Expression.Constant(runner), "RunWithResult", null, Expression.Constant(this));
        }

        public virtual bool Equals(SqlDeleteStatement other)
        {
            if (other is SqlDeleteStatement stmt)
            {
                if (WhereClause == null && stmt.WhereClause != null)
                    return false;
                if (WhereClause != null && !WhereClause.Equals(stmt.WhereClause))
                    return false;
                return TableName.Equals(stmt.TableName);
            }
            return base.Equals(other);
        }

        public override bool Equals(SqlStatement obj)
        {
            if (obj is SqlDeleteStatement item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}
