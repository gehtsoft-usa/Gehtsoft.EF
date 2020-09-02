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
            if (!checkOperationAndType(operation, mOperand.ResultType))
            {
                throw new SqlParserException(new SqlError(source,
                    operand.Position.Line,
                    operand.Position.Column,
                    $"Type of operand doesn't match the operation {operand.Symbol.Name} ({operand.Value ?? "null"})"));
            }
            if (operation == OperationType.IsNotNull || operation == OperationType.IsNull)
            {
                mResultType = ResultTypes.Boolean;
            }
            else
            {
                mResultType = mOperand.ResultType;
            }
            mOperation = operation;
        }

        internal SqlUnarExpression(SqlBaseExpression operand, OperationType operation, SqlBaseExpression rightOperand)
        {
            mOperand = operand;
            if (!checkOperationAndType(operation, mOperand.ResultType))
            {
                throw new SqlParserException(new SqlError(null, 0, 0, $"Type of operand doesn't match the operation"));
            }
            if (operation == OperationType.IsNotNull || operation == OperationType.IsNull)
            {
                mResultType = ResultTypes.Boolean;
            }
            else
            {
                mResultType = mOperand.ResultType;
            }
            mOperation = operation;
        }

        private bool checkOperationAndType(OperationType operation, ResultTypes resultType)
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
            return isCorrect;
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
