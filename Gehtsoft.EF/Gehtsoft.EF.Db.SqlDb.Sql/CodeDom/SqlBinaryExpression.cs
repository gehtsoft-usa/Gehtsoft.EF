using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class SqlBinaryExpression : SqlBaseExpression
    {
        private ResultTypes mResultType = ResultTypes.Unknown;
        private SqlBaseExpression mLeftOperand;
        private SqlBaseExpression mRightOperand;
        private OperationType mOperation;

        /// <summary>
        /// The types of the Operation
        /// </summary>
        public enum OperationType
        {
            Or,
            And,
            Gt,
            Ge,
            Lt,
            Le,
            Eq,
            Neq,
            Plus,
            Minus,
            Mult,
            Div,
            Concat,
        };

        public override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.Binary;
            }
        }
        public override ResultTypes ResultType
        {
            get
            {
                return mResultType;
            }
        }

        public SqlBaseExpression LeftOperand
        {
            get
            {
                return mLeftOperand;
            }
        }

        public SqlBaseExpression RightOperand
        {
            get
            {
                return mRightOperand;
            }
        }

        public OperationType Operation
        {
            get
            {
                return mOperation;
            }
        }

        internal SqlBinaryExpression(SqlStatement parentStatement, ASTNode leftOperand, OperationType operation, ASTNode rightOperand, string source)
        {
            mLeftOperand = SqlExpressionParser.ParseExpression(parentStatement, leftOperand, source);
            mRightOperand = SqlExpressionParser.ParseExpression(parentStatement, rightOperand, source);
            if (mLeftOperand.ResultType != ResultTypes.Unknown && mRightOperand.ResultType != ResultTypes.Unknown)
            {
                if (mLeftOperand.ResultType != mRightOperand.ResultType || !checkOperationAndType(operation, mLeftOperand.ResultType))
                {
                    throw new SqlParserException(new SqlError(source,
                        rightOperand.Position.Line,
                        rightOperand.Position.Column,
                        $"Incorrect type of operand {rightOperand.Symbol.Name} ({rightOperand.Value ?? "null"})"));
                }
                mResultType = getResultType(operation, mLeftOperand.ResultType);
            }
            mOperation = operation;
        }

        internal SqlBinaryExpression(SqlBaseExpression leftOperand, OperationType operation, SqlBaseExpression rightOperand)
        {
            mLeftOperand = leftOperand;
            mRightOperand = rightOperand;
            if (mLeftOperand.ResultType != ResultTypes.Unknown && mRightOperand.ResultType != ResultTypes.Unknown)
            {
                if (mLeftOperand.ResultType != mRightOperand.ResultType || !checkOperationAndType(operation, mLeftOperand.ResultType))
                {
                    throw new SqlParserException(new SqlError(null, 0, 0, $"Types of operands don't match"));
                }
                mResultType = getResultType(operation, mLeftOperand.ResultType);
            }
            mOperation = operation;
        }

        private ResultTypes getResultType(OperationType operation, ResultTypes resultType)
        {
            ResultTypes result = resultType;
            if (operation == OperationType.Eq ||
                operation == OperationType.Neq ||
                operation == OperationType.Gt ||
                operation == OperationType.Ge ||
                operation == OperationType.Lt ||
                operation == OperationType.Le)
                result = ResultTypes.Boolean;

            return result;
        }

        private bool checkOperationAndType(OperationType operation, ResultTypes resultType)
        {
            bool isCorrect = true;
            switch (operation)
            {
                case OperationType.Minus:
                case OperationType.Plus:
                case OperationType.Mult:
                case OperationType.Div:
                    isCorrect = resultType == ResultTypes.Double || resultType == ResultTypes.Integer;
                    break;
                case OperationType.Concat:
                    isCorrect = resultType == ResultTypes.String;
                    break;
                case OperationType.And:
                case OperationType.Or:
                    isCorrect = resultType == ResultTypes.Boolean;
                    break;
            }
            return isCorrect;
        }

        public virtual bool Equals(SqlBinaryExpression other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            return (this.LeftOperand.Equals(other.LeftOperand) && this.RightOperand.Equals(other.RightOperand) && this.Operation.Equals(other.Operation));
        }

        public override bool Equals(SqlBaseExpression obj)
        {
            if (obj is SqlBinaryExpression item)
                return Equals(item);
            return base.Equals(obj);
        }

    }
}
