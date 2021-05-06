using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class AssignExpression : SqlBaseExpression
    {
        private readonly ResultTypes mResultType = ResultTypes.Unknown;

        internal override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.Assign;
            }
        }
        internal override ResultTypes ResultType
        {
            get
            {
                return mResultType;
            }
        }

        internal GlobalParameter LeftOperand { get; }

        internal SqlBaseExpression RightOperand { get; }
        private readonly SqlCodeDomBuilder mCodeDomBuilder;

        internal AssignExpression(Statement parentStatement, ASTNode leftOperand, ASTNode rightOperand, string source)
        {
            mCodeDomBuilder = parentStatement.CodeDomBuilder;
            LeftOperand = (GlobalParameter)SqlExpressionParser.ParseExpression(parentStatement, leftOperand, source);
            RightOperand = SqlExpressionParser.ParseExpression(parentStatement, rightOperand, source);
            if (!Statement.IsCalculable(RightOperand))
            {
                throw new SqlParserException(new SqlError(source,
                    rightOperand.Position.Line,
                    rightOperand.Position.Column,
                    "Not calculable expression in assign statement"));
            }
            CheckLeftOperand();
            CheckOperands(LeftOperand, RightOperand, source, rightOperand.Position.Line, rightOperand.Position.Column);
            mResultType = RightOperand.ResultType;
        }

        internal AssignExpression(SqlCodeDomBuilder builder, GlobalParameter leftOperand, SqlBaseExpression rightOperand)
        {
            mCodeDomBuilder = builder;
            LeftOperand = leftOperand;
            RightOperand = rightOperand;
            if (!Statement.IsCalculable(RightOperand))
            {
                throw new SqlParserException(new SqlError(null, 0, 0, "Not calculable expression in assign statement"));
            }
            CheckLeftOperand();
            CheckOperands(LeftOperand, RightOperand);
            mResultType = RightOperand.ResultType;
        }

        private void CheckLeftOperand()
        {
            SqlBaseExpression existing = mCodeDomBuilder.FindGlobalParameter(LeftOperand.Name);
            if (existing == null)
            {
                mCodeDomBuilder.AddGlobalParameter(LeftOperand.Name, RightOperand.ResultType);
                LeftOperand.ResetResultType();
            }
        }

        private static void CheckOperands(SqlBaseExpression leftOperand, SqlBaseExpression rightOperand,
            string source = null, int line = 0, int column = 0)
        {
            if (leftOperand.ResultType != rightOperand.ResultType)
            {
                if (!((leftOperand.ResultType == ResultTypes.Integer || leftOperand.ResultType == ResultTypes.Double) &&
                   (rightOperand.ResultType == ResultTypes.Integer || rightOperand.ResultType == ResultTypes.Double)))
                    throw new SqlParserException(new SqlError(source, line, column, "Types of operands don't match"));
            }
        }
    }
}
