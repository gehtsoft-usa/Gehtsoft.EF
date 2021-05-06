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

        internal SqlInExpression(Statement parentStatement, ASTNode leftOperand, OperationType operation, ASTNode rightOperand, string source)
        {
            LeftOperand = SqlExpressionParser.ParseExpression(parentStatement, leftOperand, source);
            if (rightOperand.Symbol.ID == SqlParser.ID.VariableInValueArgs)
            {
                RightOperandAsList = new SqlBaseExpressionCollection();
                foreach (ASTNode node in rightOperand.Children)
                {
                    SqlBaseExpression item = SqlExpressionParser.ParseExpression(parentStatement, node, source);
                    if (LeftOperand.ResultType != item.ResultType)
                    {
                        if (!((LeftOperand.ResultType == ResultTypes.Integer || LeftOperand.ResultType == ResultTypes.Double) &&
                           (item.ResultType == ResultTypes.Integer || item.ResultType == ResultTypes.Double)))
                            throw new SqlParserException(new SqlError(source,
                                rightOperand.Position.Line,
                                rightOperand.Position.Column,
                                $"Incorrect type of operand {rightOperand.Symbol.Name} ({rightOperand.Value ?? "null"})"));
                    }
                    RightOperandAsList.Add(item);
                }
            }
            else if (rightOperand.Symbol.ID == SqlParser.ID.VariableSelect)
            {
                RightOperandAsSelect = new SqlSelectStatement(parentStatement.CodeDomBuilder, rightOperand, source);
                if (RightOperandAsSelect.SelectList.FieldAliasCollection.Count != 1)
                {
                    throw new SqlParserException(new SqlError(source,
                        rightOperand.Position.Line,
                        rightOperand.Position.Column,
                        $"Expected 1 column in inner SELECT {rightOperand.Symbol.Name} ({rightOperand.Value ?? "null"})"));
                }
                ResultTypes selectExptType = RightOperandAsSelect.SelectList.FieldAliasCollection[0].Expression.ResultType;
                if (LeftOperand.ResultType != selectExptType)
                {
                    if (!((LeftOperand.ResultType == ResultTypes.Integer || LeftOperand.ResultType == ResultTypes.Double) &&
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
            Operation = operation;
        }

        internal SqlInExpression(SqlBaseExpression leftOperand, OperationType operation, SqlBaseExpressionCollection rightOperand)
        {
            LeftOperand = leftOperand;
            RightOperandAsList = rightOperand;
            Operation = operation;
        }

        internal SqlInExpression(SqlBaseExpression leftOperand, OperationType operation, SqlSelectStatement rightOperand)
        {
            LeftOperand = leftOperand;
            RightOperandAsSelect = rightOperand;
            Operation = operation;
        }
    }
}
