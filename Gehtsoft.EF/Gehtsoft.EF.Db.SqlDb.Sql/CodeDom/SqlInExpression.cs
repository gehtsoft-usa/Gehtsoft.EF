namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlInExpression : SqlBaseExpression
    {
        /// <summary>
        /// The types of the Operation
        /// </summary>
        internal enum OperationType
        {
            In,
            NotIn,
        };

        internal override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.In;
            }
        }
        internal override ResultTypes ResultType => ResultTypes.Boolean;

        internal SqlBaseExpression LeftOperand { get; }

        internal SqlBaseExpressionCollection RightOperandAsList { get; }

        internal SqlSelectStatement RightOperandAsSelect { get; }

        internal OperationType Operation { get; }

        internal SqlInExpression(Statement parentStatement, SqlParser.ExprContext leftOperand, OperationType operation, SqlParser.InPredicateValueContext rightOperand, string source)
        {
            LeftOperand = SqlExpressionParser.ParseExpression(parentStatement, leftOperand, source);
            if (rightOperand is SqlParser.InListContext listNode)
            {
                RightOperandAsList = new SqlBaseExpressionCollection();
                foreach (SqlParser.ExprContext node in listNode.inValueList().inValueArgs().expr())
                {
                    SqlBaseExpression item = SqlExpressionParser.ParseExpression(parentStatement, node, source);
                    if (LeftOperand.ResultType != item.ResultType)
                    {
                        if (!((LeftOperand.ResultType == ResultTypes.Integer || LeftOperand.ResultType == ResultTypes.Double) &&
                           (item.ResultType == ResultTypes.Integer || item.ResultType == ResultTypes.Double)))
                            throw new SqlParserException(new SqlError(source,
                                rightOperand.Line(), rightOperand.Col(),
                                $"Incorrect type of operand ({rightOperand.GetText()})"));
                    }
                    RightOperandAsList.Add(item);
                }
            }
            else if (rightOperand is SqlParser.InSelectContext selectNode)
            {
                RightOperandAsSelect = new SqlSelectStatement(parentStatement.CodeDomBuilder, selectNode.selectStatement(), source);
                if (RightOperandAsSelect.SelectList.FieldAliasCollection.Count != 1)
                {
                    throw new SqlParserException(new SqlError(source,
                        rightOperand.Line(), rightOperand.Col(),
                        $"Expected 1 column in inner SELECT ({rightOperand.GetText()})"));
                }
                ResultTypes selectExptType = RightOperandAsSelect.SelectList.FieldAliasCollection[0].Expression.ResultType;
                if (LeftOperand.ResultType != selectExptType)
                {
                    if (!((LeftOperand.ResultType == ResultTypes.Integer || LeftOperand.ResultType == ResultTypes.Double) &&
                       (selectExptType == ResultTypes.Integer || selectExptType == ResultTypes.Double)))
                        throw new SqlParserException(new SqlError(source,
                            rightOperand.Line(), rightOperand.Col(),
                            $"Incorrect type of operand ({rightOperand.GetText()})"));
                }
            }
            else
            {
                throw new SqlParserException(new SqlError(source,
                    rightOperand.Line(), rightOperand.Col(),
                    $"Incorrect type of IN right operand ({rightOperand.GetText()})"));
            }
            Operation = operation;
        }
    }
}
