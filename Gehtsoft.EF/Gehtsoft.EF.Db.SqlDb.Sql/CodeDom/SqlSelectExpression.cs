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
    internal class SqlSelectExpression : SqlBaseExpression
    {
        private readonly ResultTypes mResultType = ResultTypes.Unknown;
        internal SqlSelectStatement SelectStatement { get; } = null;

        internal override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.SelectExpression;
            }
        }
        internal override ResultTypes ResultType
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
    }
}
