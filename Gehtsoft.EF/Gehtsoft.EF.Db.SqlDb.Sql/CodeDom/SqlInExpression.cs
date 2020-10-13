using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlInExpression : SqlBaseExpression
    {
        private readonly ResultTypes mResultType = ResultTypes.Boolean;
        private SqlBaseExpression mLeftOperand;
        private SqlBaseExpressionCollection mRightList = null;
        private SqlSelectStatement mRightSelect = null;
        private OperationType mOperation;

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
        internal override ResultTypes ResultType
        {
            get
            {
                return mResultType;
            }
        }

        internal SqlBaseExpression LeftOperand
        {
            get
            {
                return mLeftOperand;
            }
        }

        internal SqlBaseExpressionCollection RightOperandAsList
        {
            get
            {
                return mRightList;
            }
        }

        internal SqlSelectStatement RightOperandAsSelect
        {
            get
            {
                return mRightSelect;
            }
        }

        internal OperationType Operation
        {
            get
            {
                return mOperation;
            }
        }

        internal SqlInExpression(Statement parentStatement, ASTNode leftOperand, OperationType operation, ASTNode rightOperand, string source)
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
    }
}
