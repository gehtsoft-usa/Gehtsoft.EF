﻿using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlUnarExpression : SqlBaseExpression
    {
        private ResultTypes mResultType = ResultTypes.Unknown;
        private SqlBaseExpression mOperand;
        private OperationType mOperation;

        /// <summary>
        /// The types of the Operation
        /// </summary>
        internal enum OperationType
        {
            Plus,
            Minus,
            Not,
            IsNull,
            IsNotNull
        };

        internal override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.Unar;
            }
        }
        internal override ResultTypes ResultType
        {
            get
            {
                return mResultType;
            }
        }

        internal SqlBaseExpression Operand
        {
            get
            {
                return mOperand;
            }
        }

        internal OperationType Operation
        {
            get
            {
                return mOperation;
            }
        }

        internal SqlUnarExpression(SqlStatement parentStatement, ASTNode operand, OperationType operation, string source)
        {
            mOperand = SqlExpressionParser.ParseExpression(parentStatement, operand, source);
            mOperation = operation;
            CheckOperationAndType(operation, mOperand, source, operand.Position.Line, operand.Position.Column);
            mResultType = prepareResultType(mOperand, operation);
        }

        internal SqlUnarExpression(SqlBaseExpression operand, OperationType operation)
        {
            mOperand = operand;
            mOperation = operation;
            CheckOperationAndType(operation, mOperand);
            mResultType = prepareResultType(operand, operation);
        }
        internal static SqlConstant TryGetConstant(SqlBaseExpression operand, OperationType operation)
        {
            SqlConstant result = null;

            CheckOperationAndType(operation, operand);
            if (operand is SqlConstant constant)
            {
                object value = null;
                ResultTypes type = prepareResultType(operand, operation);
                switch (operation)
                {
                    case OperationType.IsNull:
                        value = constant.Value == null;
                        break;
                    case OperationType.IsNotNull:
                        value = constant.Value != null;
                        break;
                    default:
                        switch (constant.ResultType)
                        {
                            case ResultTypes.Integer:
                                switch (operation)
                                {
                                    case OperationType.Plus:
                                        value = (int)constant.Value;
                                        break;
                                    case OperationType.Minus:
                                        value = 0 - (int)constant.Value;
                                        break;
                                }
                                break;
                            case ResultTypes.Double:
                                switch (operation)
                                {
                                    case OperationType.Plus:
                                        value = (double)constant.Value;
                                        break;
                                    case OperationType.Minus:
                                        value = 0.0 - (double)constant.Value;
                                        break;
                                }
                                break;
                            case ResultTypes.Boolean:
                                switch (operation)
                                {
                                    case OperationType.Not:
                                        value = !((bool)constant.Value);
                                        break;
                                }
                                break;
                            case ResultTypes.Unknown:
                                switch (operation)
                                {
                                    case OperationType.IsNull:
                                        value = true;
                                        break;
                                    case OperationType.IsNotNull:
                                        value = false;
                                        break;
                                }
                                break;
                        }
                        break;
                }
                if (value != null)
                    result = new SqlConstant(value, type);
            }
            return result;
        }

        private static ResultTypes prepareResultType(SqlBaseExpression operand, OperationType operation)
        {
            if (operation == OperationType.IsNotNull || operation == OperationType.IsNull)
            {
                return ResultTypes.Boolean;
            }
            else
            {
                return operand.ResultType;
            }
        }

        internal static void CheckOperationAndType(OperationType operation, SqlBaseExpression operand,
            string source = null, int line = 0, int column = 0, bool checkGlobalParameters = false)
        {
            if (!checkGlobalParameters)
                if (operand.ExpressionType == ExpressionTypes.GlobalParameter)
                    return;

            ResultTypes resultType = operand.ResultType;

            bool isCorrect = false;
            switch (operation)
            {
                case OperationType.Minus:
                case OperationType.Plus:
                    isCorrect = resultType == ResultTypes.Double || resultType == ResultTypes.Integer;
                    break;
                case OperationType.Not:
                    isCorrect = resultType == ResultTypes.Boolean;
                    break;
                case OperationType.IsNotNull:
                case OperationType.IsNull:
                    isCorrect = true;
                    break;
            }
            if (!isCorrect)
            {
                throw new SqlParserException(new SqlError(source, line, column, $"Type of operand doesn't match the operation"));
            }
        }
    }
}
