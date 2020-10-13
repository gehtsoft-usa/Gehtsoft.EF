using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal  class AssignExpression : SqlBaseExpression
    {
        private ResultTypes mResultType = ResultTypes.Unknown;
        private GlobalParameter mLeftOperand;
        private SqlBaseExpression mRightOperand;

        internal  override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.Assign;
            }
        }
        internal  override ResultTypes ResultType
        {
            get
            {
                return mResultType;
            }
        }

        internal  GlobalParameter LeftOperand
        {
            get
            {
                return mLeftOperand;
            }
        }

        internal  SqlBaseExpression RightOperand
        {
            get
            {
                return mRightOperand;
            }
        }
        private SqlCodeDomBuilder mCodeDomBuilder;

        internal AssignExpression(Statement parentStatement, ASTNode leftOperand, ASTNode rightOperand, string source)
        {
            mCodeDomBuilder = parentStatement.CodeDomBuilder;
            mLeftOperand = (GlobalParameter)SqlExpressionParser.ParseExpression(parentStatement, leftOperand, source);
            mRightOperand = SqlExpressionParser.ParseExpression(parentStatement, rightOperand, source);
            if (!Statement.IsCalculable(mRightOperand))
            {
                throw new SqlParserException(new SqlError(source,
                    rightOperand.Position.Line,
                    rightOperand.Position.Column,
                    $"Not calculable expression in assign statement"));
            }
            checkLeftOperand();
            checkOperands(mLeftOperand, mRightOperand, source, rightOperand.Position.Line, rightOperand.Position.Column);
            mResultType = mRightOperand.ResultType;
        }

        internal AssignExpression(SqlCodeDomBuilder builder, GlobalParameter leftOperand, SqlBaseExpression rightOperand)
        {
            mCodeDomBuilder = builder;
            mLeftOperand = leftOperand;
            mRightOperand = rightOperand;
            if (!Statement.IsCalculable(mRightOperand))
            {
                throw new SqlParserException(new SqlError(null, 0, 0, $"Not calculable expression in assign statement"));
            }
            checkLeftOperand();
            checkOperands(mLeftOperand, mRightOperand);
            mResultType = mRightOperand.ResultType;
        }

        private void checkLeftOperand()
        {
            SqlBaseExpression existing = mCodeDomBuilder.FindGlobalParameter(mLeftOperand.Name);
            if (existing == null)
            {
                mCodeDomBuilder.AddGlobalParameter(mLeftOperand.Name, mRightOperand.ResultType);
                mLeftOperand.ResetResultType();
            }
        }

        private static void checkOperands(SqlBaseExpression leftOperand, SqlBaseExpression rightOperand,
            string source = null, int line = 0, int column = 0)
        {
            if (leftOperand.ResultType != rightOperand.ResultType)
            {
                if (!((leftOperand.ResultType == ResultTypes.Integer || leftOperand.ResultType == ResultTypes.Double) &&
                   (rightOperand.ResultType == ResultTypes.Integer || rightOperand.ResultType == ResultTypes.Double)))
                    throw new SqlParserException(new SqlError(source, line, column, $"Types of operands don't match"));
            }
        }
    }
}
