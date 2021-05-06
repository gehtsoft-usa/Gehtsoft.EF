using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlBinaryExpression : SqlBaseExpression
    {
        private readonly ResultTypes mResultType;

        /// <summary>
        /// The types of the Operation
        /// </summary>
        internal enum OperationType
        {
            Or,
            And,
            Gt,
            Ge,
            Ls,
            Le,
            Eq,
            Neq,
            Plus,
            Minus,
            Mult,
            Div,
            Concat,
        };

        internal override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.Binary;
            }
        }
        internal override ResultTypes ResultType
        {
            get
            {
                return mResultType;
            }
        }

        internal SqlBaseExpression LeftOperand { get; }

        internal SqlBaseExpression RightOperand { get; }

        internal OperationType Operation { get; }

        internal SqlBinaryExpression(SqlStatement parentStatement, ASTNode leftOperand, OperationType operation, ASTNode rightOperand, string source)
        {
            LeftOperand = SqlExpressionParser.ParseExpression(parentStatement, leftOperand, source);
            RightOperand = SqlExpressionParser.ParseExpression(parentStatement, rightOperand, source);

            CheckOperands(LeftOperand, operation, RightOperand, source, rightOperand.Position.Line, rightOperand.Position.Column);
            mResultType = GetResultType(operation, LeftOperand.ResultType);
            Operation = operation;
        }

        internal SqlBinaryExpression(SqlBaseExpression leftOperand, OperationType operation, SqlBaseExpression rightOperand)
        {
            LeftOperand = leftOperand;
            RightOperand = rightOperand;
            CheckOperands(LeftOperand, operation, RightOperand);
            mResultType = GetResultType(operation, LeftOperand.ResultType);
            Operation = operation;
        }

        internal static SqlConstant TryGetConstant(SqlBaseExpression leftOperand, OperationType operation, SqlBaseExpression rightOperand)
        {
            SqlConstant result = null;

            CheckOperands(leftOperand, operation, rightOperand);
            if (leftOperand is SqlConstant leftConstant && rightOperand is SqlConstant rightConstant)
            {
                object value = null;
                ResultTypes type = ResultTypes.Unknown;
                if (leftConstant.ResultType == rightConstant.ResultType)
                {
                    switch (leftConstant.ResultType)
                    {
                        case ResultTypes.Integer:
                            switch (operation)
                            {
                                case OperationType.Eq:
                                    value = (int)leftConstant.Value == (int)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Neq:
                                    value = (int)leftConstant.Value != (int)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Gt:
                                    value = (int)leftConstant.Value > (int)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Ge:
                                    value = (int)leftConstant.Value >= (int)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Ls:
                                    value = (int)leftConstant.Value < (int)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Le:
                                    value = (int)leftConstant.Value <= (int)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;

                                case OperationType.Plus:
                                    value = (int)leftConstant.Value + (int)rightConstant.Value;
                                    type = ResultTypes.Integer;
                                    break;
                                case OperationType.Minus:
                                    value = (int)leftConstant.Value - (int)rightConstant.Value;
                                    type = ResultTypes.Integer;
                                    break;
                                case OperationType.Mult:
                                    value = (int)leftConstant.Value * (int)rightConstant.Value;
                                    type = ResultTypes.Integer;
                                    break;
                                case OperationType.Div:
                                    value = (int)leftConstant.Value / (int)rightConstant.Value;
                                    type = ResultTypes.Integer;
                                    break;
                            }
                            break;
                        case ResultTypes.Double:
                            switch (operation)
                            {
                                case OperationType.Eq:
                                    value = (double)leftConstant.Value == (double)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Neq:
                                    value = (double)leftConstant.Value != (double)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Gt:
                                    value = (double)leftConstant.Value > (double)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Ge:
                                    value = (double)leftConstant.Value >= (double)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Ls:
                                    value = (double)leftConstant.Value < (double)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Le:
                                    value = (double)leftConstant.Value <= (double)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;

                                case OperationType.Plus:
                                    value = (double)leftConstant.Value + (double)rightConstant.Value;
                                    type = ResultTypes.Double;
                                    break;
                                case OperationType.Minus:
                                    value = (double)leftConstant.Value - (double)rightConstant.Value;
                                    type = ResultTypes.Double;
                                    break;
                                case OperationType.Mult:
                                    value = (double)leftConstant.Value * (double)rightConstant.Value;
                                    type = ResultTypes.Double;
                                    break;
                                case OperationType.Div:
                                    value = (double)leftConstant.Value / (double)rightConstant.Value;
                                    type = ResultTypes.Double;
                                    break;
                            }
                            break;
                        case ResultTypes.Boolean:
                            switch (operation)
                            {
                                case OperationType.Eq:
                                    value = (bool)leftConstant.Value == (bool)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Neq:
                                    value = (bool)leftConstant.Value != (bool)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;

                                case OperationType.And:
                                    value = (bool)leftConstant.Value && (bool)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Or:
                                    value = (bool)leftConstant.Value || (bool)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                            }
                            break;
                        case ResultTypes.DateTime:
                            switch (operation)
                            {
                                case OperationType.Eq:
                                    value = (DateTime)leftConstant.Value == (DateTime)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Neq:
                                    value = (DateTime)leftConstant.Value != (DateTime)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Gt:
                                    value = (DateTime)leftConstant.Value > (DateTime)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Ge:
                                    value = (DateTime)leftConstant.Value >= (DateTime)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Ls:
                                    value = (DateTime)leftConstant.Value < (DateTime)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Le:
                                    value = (DateTime)leftConstant.Value <= (DateTime)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                            }
                            break;
                        case ResultTypes.String:
                            switch (operation)
                            {
                                case OperationType.Eq:
                                    value = (string)leftConstant.Value == (string)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Neq:
                                    value = (string)leftConstant.Value != (string)rightConstant.Value;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Gt:
                                    value = ((string)leftConstant.Value).CompareTo((string)rightConstant.Value) > 0;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Ge:
                                    value = ((string)leftConstant.Value).CompareTo((string)rightConstant.Value) >= 0;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Ls:
                                    value = ((string)leftConstant.Value).CompareTo((string)rightConstant.Value) < 0;
                                    type = ResultTypes.Boolean;
                                    break;
                                case OperationType.Le:
                                    value = ((string)leftConstant.Value).CompareTo((string)rightConstant.Value) <= 0;
                                    type = ResultTypes.Boolean;
                                    break;

                                case OperationType.Concat:
                                    value = (string)leftConstant.Value + (string)rightConstant.Value;
                                    type = ResultTypes.String;
                                    break;
                            }
                            break;
                    }
                }
                else
                {
                    if ((leftConstant.ResultType == ResultTypes.Integer || leftConstant.ResultType == ResultTypes.Double) &&
                       (rightConstant.ResultType == ResultTypes.Integer || rightConstant.ResultType == ResultTypes.Double))
                    {
                        string leftStr = leftConstant.Value.ToString();
                        string rightStr = rightConstant.Value.ToString();
                        double leftValue = double.Parse(leftStr);
                        double rightValue = double.Parse(rightStr);
                        switch (operation)
                        {
                            case OperationType.Eq:
                                value = leftValue == rightValue;
                                type = ResultTypes.Boolean;
                                break;
                            case OperationType.Neq:
                                value = leftValue != rightValue;
                                type = ResultTypes.Boolean;
                                break;
                            case OperationType.Gt:
                                value = leftValue > rightValue;
                                type = ResultTypes.Boolean;
                                break;
                            case OperationType.Ge:
                                value = leftValue >= rightValue;
                                type = ResultTypes.Boolean;
                                break;
                            case OperationType.Ls:
                                value = leftValue < rightValue;
                                type = ResultTypes.Boolean;
                                break;
                            case OperationType.Le:
                                value = leftValue <= rightValue;
                                type = ResultTypes.Boolean;
                                break;

                            case OperationType.Plus:
                                value = leftValue + rightValue;
                                type = ResultTypes.Double;
                                break;
                            case OperationType.Minus:
                                value = leftValue - rightValue;
                                type = ResultTypes.Double;
                                break;
                            case OperationType.Mult:
                                value = leftValue * rightValue;
                                type = ResultTypes.Double;
                                break;
                            case OperationType.Div:
                                value = leftValue / rightValue;
                                type = ResultTypes.Double;
                                break;
                        }
                    }
                }
                if (value != null)
                    result = new SqlConstant(value, type);
            }

            return result;
        }

        internal static void CheckOperands(SqlBaseExpression leftOperand, OperationType operation, SqlBaseExpression rightOperand,
            string source = null, int line = 0, int column = 0, bool checkGlobalParameters = false)
        {
            if (!checkGlobalParameters)
                if (leftOperand.ExpressionType == ExpressionTypes.GlobalParameter ||
                    rightOperand.ExpressionType == ExpressionTypes.GlobalParameter)
                    return;

            if (leftOperand.ResultType != rightOperand.ResultType)
            {
                if (!((leftOperand.ResultType == ResultTypes.Integer || leftOperand.ResultType == ResultTypes.Double) &&
                   (rightOperand.ResultType == ResultTypes.Integer || rightOperand.ResultType == ResultTypes.Double)))
                    throw new SqlParserException(new SqlError(source, line, column, "Types of operands don't match"));
            }
            if (!CheckOperationAndType(operation, leftOperand.ResultType))
            {
                throw new SqlParserException(new SqlError(source, line, column,
                    $"Incorrect type of operation '{operation}' for type '{leftOperand.ResultType}')"));
            }
        }

        private static ResultTypes GetResultType(OperationType operation, ResultTypes resultType)
        {
            ResultTypes result = resultType;
            if (operation == OperationType.Eq ||
                operation == OperationType.Neq ||
                operation == OperationType.Gt ||
                operation == OperationType.Ge ||
                operation == OperationType.Ls ||
                operation == OperationType.Le)
                result = ResultTypes.Boolean;

            return result;
        }

        private static bool CheckOperationAndType(OperationType operation, ResultTypes resultType)
        {
            switch (operation)
            {
                case OperationType.Minus:
                case OperationType.Plus:
                case OperationType.Mult:
                case OperationType.Div:
                    return resultType == ResultTypes.Double || resultType == ResultTypes.Integer;
                case OperationType.Concat:
                    return resultType == ResultTypes.String;
                case OperationType.And:
                case OperationType.Or:
                    return resultType == ResultTypes.Boolean;
            }
            return true;
        }
    }
}
