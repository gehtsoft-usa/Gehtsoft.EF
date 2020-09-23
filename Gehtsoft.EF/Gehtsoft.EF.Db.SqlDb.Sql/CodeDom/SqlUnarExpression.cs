using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class SqlUnarExpression : SqlBaseExpression
    {
        private ResultTypes mResultType = ResultTypes.Unknown;
        private SqlBaseExpression mOperand;
        private OperationType mOperation;

        /// <summary>
        /// The types of the Operation
        /// </summary>
        public enum OperationType
        {
            Plus,
            Minus,
            Not,
            IsNull,
            IsNotNull
        };

        public override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.Unar;
            }
        }
        public override ResultTypes ResultType
        {
            get
            {
                return mResultType;
            }
        }

        public SqlBaseExpression Operand
        {
            get
            {
                return mOperand;
            }
        }

        public OperationType Operation
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
            checkOperationAndType(operation, mOperand.ResultType, source, operand.Position.Line, operand.Position.Column);
            mResultType = prepareResultType(mOperand, operation);
        }

        internal SqlUnarExpression(SqlBaseExpression operand, OperationType operation)
        {
            mOperand = operand;
            mOperation = operation;
            checkOperationAndType(operation, mOperand.ResultType);
            mResultType = prepareResultType(operand, operation);
        }
        internal static SqlConstant TryGetConstant(SqlBaseExpression operand, OperationType operation)
        {
            SqlConstant result = null;

            checkOperationAndType(operation, operand.ResultType);
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

        private static void checkOperationAndType(OperationType operation, ResultTypes resultType,
            string source = null, int line = 0, int column = 0)
        {
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

        public virtual bool Equals(SqlUnarExpression other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            return (this.Operand.Equals(other.Operand) && this.Operation.Equals(other.Operation));
        }

        public override bool Equals(SqlBaseExpression obj)
        {
            if (obj is SqlUnarExpression item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}
