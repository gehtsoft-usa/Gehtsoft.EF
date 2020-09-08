using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class SqlInExpression : SqlBaseExpression
    {
        private readonly ResultTypes mResultType = ResultTypes.Boolean;
        private SqlBaseExpression mLeftOperand;
        private SqlBaseExpressionCollection mRightList = null;
        private SqlSelectStatement mRightSelect = null;
        private OperationType mOperation;

        /// <summary>
        /// The types of the Operation
        /// </summary>
        public enum OperationType
        {
            In,
            NotIn,
        };

        public override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.In;
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

        public SqlBaseExpressionCollection RightOperandAsList
        {
            get
            {
                return mRightList;
            }
        }

        public SqlSelectStatement RightOperandAsSelect
        {
            get
            {
                return mRightSelect;
            }
        }

        public OperationType Operation
        {
            get
            {
                return mOperation;
            }
        }

        internal SqlInExpression(SqlStatement parentStatement, ASTNode leftOperand, OperationType operation, ASTNode rightOperand, string source)
        {
            mLeftOperand = SqlExpressionParser.ParseExpression(parentStatement, leftOperand, source);
            if (rightOperand.Symbol.ID == SqlParser.ID.VariableInValueArgs)
            {
                mRightList = new SqlBaseExpressionCollection();
                foreach (ASTNode node in rightOperand.Children)
                {
                    SqlBaseExpression item = SqlExpressionParser.ParseExpression(parentStatement, node, source);
                    if (mLeftOperand.ResultType != item.ResultType)
                    {
                        if (!((mLeftOperand.ResultType == ResultTypes.Integer || mLeftOperand.ResultType == ResultTypes.Double) &&
                           (item.ResultType == ResultTypes.Integer || item.ResultType == ResultTypes.Double)))
                            throw new SqlParserException(new SqlError(source,
                                rightOperand.Position.Line,
                                rightOperand.Position.Column,
                                $"Incorrect type of operand {rightOperand.Symbol.Name} ({rightOperand.Value ?? "null"})"));
                    }
                    mRightList.Add(item);
                }
            }
            else if (rightOperand.Symbol.ID == SqlParser.ID.VariableSelect)
            {
                mRightSelect = new SqlSelectStatement(parentStatement.CodeDomBuilder, rightOperand, source);
                if (mRightSelect.SelectList.FieldAliasCollection.Count != 1)
                {
                    throw new SqlParserException(new SqlError(source,
                        rightOperand.Position.Line,
                        rightOperand.Position.Column,
                        $"Expected 1 column in inner SELECT {rightOperand.Symbol.Name} ({rightOperand.Value ?? "null"})"));
                }
                ResultTypes selectExptType = mRightSelect.SelectList.FieldAliasCollection[0].Expression.ResultType;
                if (mLeftOperand.ResultType != selectExptType)
                {
                    if (!((mLeftOperand.ResultType == ResultTypes.Integer || mLeftOperand.ResultType == ResultTypes.Double) &&
                       (selectExptType == ResultTypes.Integer || selectExptType == ResultTypes.Double)))
                        throw new SqlParserException(new SqlError(source,
                            rightOperand.Position.Line,
                            rightOperand.Position.Column,
                            $"Incorrect type of operand {rightOperand.Symbol.Name} ({rightOperand.Value ?? "null"})"));
                }
            }
            else
            {
                throw new SqlParserException(new SqlError(source,
                    rightOperand.Position.Line,
                    rightOperand.Position.Column,
                    $"Incorrect type of IN right operand {rightOperand.Symbol.Name} ({rightOperand.Value ?? "null"})"));
            }
            mOperation = operation;
        }

        internal SqlInExpression(SqlBaseExpression leftOperand, OperationType operation, SqlBaseExpressionCollection rightOperand)
        {
            mLeftOperand = leftOperand;
            mRightList = rightOperand;
            mOperation = operation;
        }

        internal SqlInExpression(SqlBaseExpression leftOperand, OperationType operation, SqlSelectStatement rightOperand)
        {
            mLeftOperand = leftOperand;
            mRightSelect = rightOperand;
            mOperation = operation;
        }

        public virtual bool Equals(SqlInExpression other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            if (RightOperandAsList != null)
            {
                if (!RightOperandAsList.Equals(other.RightOperandAsList)) return false;
            }
            else if (RightOperandAsSelect != null)
            {
                if (!RightOperandAsSelect.Equals(other.RightOperandAsSelect)) return false;
            }
            return (this.LeftOperand.Equals(other.LeftOperand) && this.Operation.Equals(other.Operation));
        }

        public override bool Equals(SqlBaseExpression obj)
        {
            if (obj is SqlInExpression item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}
