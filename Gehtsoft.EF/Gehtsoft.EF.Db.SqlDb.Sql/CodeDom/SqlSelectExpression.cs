using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlStatement;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class SqlSelectExpression : SqlBaseExpression
    {
        private ResultTypes mResultType = ResultTypes.Unknown;
        public SqlSelectStatement SelectStatement { get; } = null;

        public override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.SelectExpression;
            }
        }
        public override ResultTypes ResultType
        {
            get
            {
                return mResultType;
            }
        }

        internal SqlSelectExpression(Statement parentStatement, ASTNode exprNode, string source)
        {
            SelectStatement = new SqlSelectStatement(parentStatement.CodeDomBuilder, exprNode.Children[0], source);
            if (SelectStatement.SelectList.FieldAliasCollection.Count != 1)
            {
                throw new SqlParserException(new SqlError(source,
                    exprNode.Children[0].Position.Line,
                    exprNode.Children[0].Position.Column,
                    $"Expected 1 column in inner SELECT {exprNode.Children[0].Symbol.Name} ({exprNode.Children[0].Value ?? "null"})"));
            }

            mResultType = SelectStatement.SelectList.FieldAliasCollection[0].Expression.ResultType;
        }

        public virtual bool Equals(SqlSelectExpression other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            return this.SelectStatement.Equals(other.SelectStatement);
        }

        public override bool Equals(SqlBaseExpression obj)
        {
            if (obj is SqlSelectExpression item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}
