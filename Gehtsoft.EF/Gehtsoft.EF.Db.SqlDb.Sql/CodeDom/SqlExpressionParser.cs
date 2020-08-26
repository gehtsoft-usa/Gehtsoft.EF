using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public static class SqlExpressionParser
    {
        public static SqlBaseExpression ParseExpression(SqlStatement parentStatement, ASTNode fieldNode, string source)
        {
            SqlBaseExpression result = null;
            string operation = string.Empty;
            ResultTypes opType = ResultTypes.Unknown;
            object constant = null;
            SqlBinaryExpression.OperationType? binaryOp = null;
            SqlUnarExpression.OperationType? unarOp = null;
            string funcName = null;
            SqlBaseExpressionCollection callParameters = null;
            ResultTypes funcResultType = ResultTypes.Unknown;

            switch (fieldNode.Symbol.ID)
            {
                case SqlParser.ID.VariableField:
                    result = new SqlField(parentStatement, fieldNode, source);
                    break;
                case SqlLexer.ID.TerminalInteger:
                    opType = ResultTypes.Integer;
                    constant = int.Parse(fieldNode.Value);
                    break;
                case SqlLexer.ID.TerminalReal:
                    opType = ResultTypes.Double;
                    constant = double.Parse(fieldNode.Value);
                    break;
                case SqlLexer.ID.TerminalStringdq:
                    opType = ResultTypes.String;
                    constant = fieldNode.Value.Substring(1, fieldNode.Value.Length - 2);
                    break;
                case SqlLexer.ID.TerminalStringsq:
                    opType = ResultTypes.String;
                    constant = fieldNode.Value.Substring(1, fieldNode.Value.Length - 2);
                    break;
                case SqlParser.ID.VariableBooleanTrue:
                    opType = ResultTypes.Boolean;
                    constant = true;
                    break;
                case SqlParser.ID.VariableBooleanFalse:
                    opType = ResultTypes.Boolean;
                    constant = false;
                    break;
                case SqlParser.ID.VariableDateConst:
                    DateTime dt;
                    if (!DateTime.TryParseExact(fieldNode.Children[0].Value.Substring(1, fieldNode.Children[0].Value.Length - 2),
                        "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                    {
                        throw new SqlParserException(new SqlError(source,
                            fieldNode.Children[0].Position.Line,
                            fieldNode.Children[0].Position.Column,
                            $"Incorrect DateTime ({fieldNode.Children[0].Value ?? "null"})"));
                    }
                    constant = dt;
                    opType = ResultTypes.Date;
                    break;
                case SqlParser.ID.VariableAndOp:
                    binaryOp = SqlBinaryExpression.OperationType.And;
                    break;
                case SqlParser.ID.VariableOrOp:
                    binaryOp = SqlBinaryExpression.OperationType.Or;
                    break;
                case SqlParser.ID.VariableGeOp:
                    binaryOp = SqlBinaryExpression.OperationType.Ge;
                    break;
                case SqlParser.ID.VariableGtOp:
                    binaryOp = SqlBinaryExpression.OperationType.Gt;
                    break;
                case SqlParser.ID.VariableLeOp:
                    binaryOp = SqlBinaryExpression.OperationType.Le;
                    break;
                case SqlParser.ID.VariableLtOp:
                    binaryOp = SqlBinaryExpression.OperationType.Ls;
                    break;
                case SqlParser.ID.VariableEqOp:
                    binaryOp = SqlBinaryExpression.OperationType.Eq;
                    break;
                case SqlParser.ID.VariableNeqOp:
                    binaryOp = SqlBinaryExpression.OperationType.Neq;
                    break;
                case SqlParser.ID.VariableConcatOp:
                    binaryOp = SqlBinaryExpression.OperationType.Concat;
                    break;
                case SqlParser.ID.VariableMinusOp:
                    if (fieldNode.Children.Count > 1)
                    {
                        binaryOp = SqlBinaryExpression.OperationType.Minus;
                    }
                    else
                    {
                        unarOp = SqlUnarExpression.OperationType.Minus;
                    }
                    break;
                case SqlParser.ID.VariablePlusOp:
                    if (fieldNode.Children.Count > 1)
                    {
                        binaryOp = SqlBinaryExpression.OperationType.Plus;
                    }
                    else
                    {
                        unarOp = SqlUnarExpression.OperationType.Plus;
                    }
                    break;
                case SqlParser.ID.VariableNotOp:
                    unarOp = SqlUnarExpression.OperationType.Not;
                    break;
                case SqlParser.ID.VariableMulOp:
                    binaryOp = SqlBinaryExpression.OperationType.Mult;
                    break;
                case SqlParser.ID.VariableDivOp:
                    binaryOp = SqlBinaryExpression.OperationType.Div;
                    break;
                case SqlParser.ID.VariableTrimCall:
                    funcName = "TRIM";
                    funcResultType = ResultTypes.String;
                    ASTNode parameterNode = fieldNode.Children[0];
                    if (fieldNode.Children.Count > 1)
                    {
                        if (fieldNode.Children[0].Symbol.ID == SqlParser.ID.VariableTrimLeading)
                            funcName = "LTRIM";
                        else if (fieldNode.Children[0].Symbol.ID == SqlParser.ID.VariableTrimTrailing)
                            funcName = "RTRIM";

                        parameterNode = fieldNode.Children[1];
                    }
                    SqlBaseExpression parameter = SqlExpressionParser.ParseExpression(parentStatement, parameterNode, source);
                    if (parameter.ResultType != ResultTypes.String)
                    {
                        throw new SqlParserException(new SqlError(source,
                            parameterNode.Position.Line,
                            parameterNode.Position.Column,
                            $"Incorrect type of parameter ({parameterNode.Value ?? "null"})"));
                    }
                    callParameters = new SqlBaseExpressionCollection();
                    callParameters.Add(parameter);

                    break;
                case SqlParser.ID.VariableAggrFunc:
                    ASTNode nameNode = fieldNode.Children[0];
                    ASTNode innerFieldNode = fieldNode.Children[1];
                    result = new SqlAggrFunc(nameNode.Value, new SqlField(parentStatement, innerFieldNode, source));
                    break;
                case SqlParser.ID.VariableAggrCountAll:
                    result = new SqlAggrFunc("COUNT", null, ResultTypes.Integer);
                    break;
            }
            if (funcName != null)
            {
                result = new SqlCallFuncExpression(funcResultType, funcName, callParameters);
            }
            if (constant != null)
            {
                result = new SqlConstant(constant, opType);
            }
            if (binaryOp.HasValue)
            {
                result = new SqlBinaryExpression(parentStatement, fieldNode.Children[0], binaryOp.Value, fieldNode.Children[1], source);
            }
            if (unarOp.HasValue)
            {
                result = new SqlUnarExpression(parentStatement, fieldNode.Children[0], unarOp.Value, source);
            }

            return result;
        }

    }
}
