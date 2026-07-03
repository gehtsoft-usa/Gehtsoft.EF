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

        internal AssignExpression(Statement parentStatement, GlobalParameter leftOperand, SqlParser.ExprContext rightOperand, string source)
        {
            mCodeDomBuilder = parentStatement.CodeDomBuilder;
            LeftOperand = leftOperand;
            RightOperand = SqlExpressionParser.ParseExpression(parentStatement, rightOperand, source);
            if (!Statement.IsCalculable(RightOperand))
            {
                throw new SqlParserException(new SqlError(source,
                    rightOperand.Line(), rightOperand.Col(),
                    "Not calculable expression in assign statement"));
            }
            CheckLeftOperand();
            CheckOperands(LeftOperand, RightOperand, source, rightOperand.Line(), rightOperand.Col());
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
