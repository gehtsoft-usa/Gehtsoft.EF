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
        public static SqlBaseExpression ParseExpression(Statement parentStatement, ASTNode fieldNode, string source)
        {
            SqlBaseExpression result = null;
            string operation = string.Empty;
            ResultTypes opType = ResultTypes.Unknown;
            object constant = null;
            SqlBinaryExpression.OperationType? binaryOp = null;
            SqlUnarExpression.OperationType? unarOp = null;
            SqlInExpression.OperationType? inOp = null;
            string funcName = null;
            SqlBaseExpressionCollection callParameters = null;
            ResultTypes funcResultType = ResultTypes.Unknown;

            switch (fieldNode.Symbol.ID)
            {
                case SqlParser.ID.VariableSelectExpr:
                    result = new SqlSelectExpression(parentStatement, fieldNode, source);
                    break;
                case SqlParser.ID.VariableField:
                    result = new SqlField(parentStatement, fieldNode, source);
                    break;
                case SqlParser.ID.VariableNull:
                    opType = ResultTypes.Unknown;
                    constant = "NULL";
                    break;
                case SqlLexer.ID.TerminalInt:
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
                    opType = ResultTypes.DateTime;
                    break;
                case SqlParser.ID.VariableDatetimeConst:
                    DateTime dtt;
                    if (!DateTime.TryParseExact(fieldNode.Children[0].Value.Substring(1, fieldNode.Children[0].Value.Length - 2),
                        "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dtt))
                    {
                        if (!DateTime.TryParseExact(fieldNode.Children[0].Value.Substring(1, fieldNode.Children[0].Value.Length - 2),
                            "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out dtt))
                        {
                            if (!DateTime.TryParseExact(fieldNode.Children[0].Value.Substring(1, fieldNode.Children[0].Value.Length - 2),
                                "yyyy-MM-dd HH", CultureInfo.InvariantCulture, DateTimeStyles.None, out dtt))
                            {
                                if (!DateTime.TryParseExact(fieldNode.Children[0].Value.Substring(1, fieldNode.Children[0].Value.Length - 2),
                                    "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dtt))
                                {
                                    throw new SqlParserException(new SqlError(source,
                                        fieldNode.Children[0].Position.Line,
                                        fieldNode.Children[0].Position.Column,
                                        $"Incorrect DateTime ({fieldNode.Children[0].Value ?? "null"})"));
                                }
                            }
                        }
                    }
                    constant = dtt;
                    opType = ResultTypes.DateTime;
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
                    SqlBaseExpression parameter = ParseExpression(parentStatement, parameterNode, source);
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
                case SqlParser.ID.VariableStrFuncCall:
                    funcName = fieldNode.Children[0].Value;
                    funcResultType = ResultTypes.String;
                    ASTNode parameterNodeStrFunc = fieldNode.Children[1];
                    SqlBaseExpression parameterStrFunc = ParseExpression(parentStatement, parameterNodeStrFunc, source);
                    if (parameterStrFunc.ResultType != ResultTypes.String)
                    {
                        throw new SqlParserException(new SqlError(source,
                            parameterNodeStrFunc.Position.Line,
                            parameterNodeStrFunc.Position.Column,
                            $"Incorrect type of parameter ({parameterNodeStrFunc.Value ?? "null"})"));
                    }
                    callParameters = new SqlBaseExpressionCollection();
                    callParameters.Add(parameterStrFunc);

                    break;
                case SqlParser.ID.VariableCastFuncCall:
                    funcName = fieldNode.Children[0].Value;
                    funcResultType = ResultTypes.String;
                    ASTNode parameterNodeCustFunc = fieldNode.Children[1];
                    switch (funcName)
                    {
                        case "TOSTRING":
                            funcResultType = ResultTypes.String;
                            break;
                        case "TOINTEGER":
                            funcResultType = ResultTypes.Integer;
                            break;
                        case "TODOUBLE":
                            funcResultType = ResultTypes.Double;
                            break;
                        case "TODATE":
                            funcResultType = ResultTypes.DateTime;
                            break;
                        case "TOTIMESTAMP":
                            funcResultType = ResultTypes.DateTime;
                            break;
                    }
                    SqlBaseExpression parameterCustFunc = ParseExpression(parentStatement, parameterNodeCustFunc, source);
                    callParameters = new SqlBaseExpressionCollection();
                    callParameters.Add(parameterCustFunc);

                    break;
                case SqlParser.ID.VariableMathFuncCall:
                    funcName = fieldNode.Children[0].Value;
                    ASTNode parameterNodeMathFunc = fieldNode.Children[1];
                    SqlBaseExpression parameterMathFunc = ParseExpression(parentStatement, parameterNodeMathFunc, source);
                    if (parameterMathFunc.ResultType != ResultTypes.Integer && parameterMathFunc.ResultType != ResultTypes.Double)
                    {
                        throw new SqlParserException(new SqlError(source,
                            parameterNodeMathFunc.Position.Line,
                            parameterNodeMathFunc.Position.Column,
                            $"Incorrect type of parameter ({parameterNodeMathFunc.Value ?? "null"})"));
                    }
                    funcResultType = parameterMathFunc.ResultType;
                    callParameters = new SqlBaseExpressionCollection();
                    callParameters.Add(parameterMathFunc);

                    break;
                case SqlParser.ID.VariableAggrFunc:
                    ASTNode nameNode = fieldNode.Children[0];
                    ASTNode innerFieldNode = fieldNode.Children[1];
                    ResultTypes? resultType = null;
                    if (nameNode.Value == "COUNT")
                    {
                        resultType = ResultTypes.Integer;
                    }
                    result = new SqlAggrFunc(nameNode.Value, new SqlField(parentStatement, innerFieldNode, source), resultType);
                    break;
                case SqlParser.ID.VariableAggrCountAll:
                    result = new SqlAggrFunc("COUNT", null, ResultTypes.Integer);
                    break;
                case SqlParser.ID.VariableExactLikeOp:
                case SqlParser.ID.VariableNotLikeOp:
                    funcName = fieldNode.Symbol.ID == SqlParser.ID.VariableExactLikeOp ? "LIKE" : "NOTLIKE";
                    funcResultType = ResultTypes.Boolean;
                    ASTNode parameter1Node = fieldNode.Children[0];
                    SqlBaseExpression parameter1 = ParseExpression(parentStatement, parameter1Node, source);
                    if (parameter1.ResultType != ResultTypes.String)
                    {
                        throw new SqlParserException(new SqlError(source,
                            parameter1Node.Position.Line,
                            parameter1Node.Position.Column,
                            $"Incorrect type of parameter ({parameter1Node.Value ?? "null"})"));
                    }
                    ASTNode parameter2Node = fieldNode.Children[1];
                    SqlBaseExpression parameter2 = ParseExpression(parentStatement, parameter2Node, source);
                    if (parameter2.ResultType != ResultTypes.String)
                    {
                        throw new SqlParserException(new SqlError(source,
                            parameter2Node.Position.Line,
                            parameter2Node.Position.Column,
                            $"Incorrect type of parameter ({parameter2Node.Value ?? "null"})"));
                    }

                    callParameters = new SqlBaseExpressionCollection();
                    callParameters.Add(parameter1);
                    callParameters.Add(parameter2);

                    break;
                case SqlParser.ID.VariableBoolStrFuncCall:
                    funcName = fieldNode.Children[0].Value;
                    funcResultType = ResultTypes.Boolean;
                    ASTNode param1Node = fieldNode.Children[1];
                    SqlBaseExpression param1 = ParseExpression(parentStatement, param1Node, source);
                    if (param1.ResultType != ResultTypes.String)
                    {
                        throw new SqlParserException(new SqlError(source,
                            param1Node.Position.Line,
                            param1Node.Position.Column,
                            $"Incorrect type of parameter ({param1Node.Value ?? "null"})"));
                    }
                    ASTNode param2Node = fieldNode.Children[2];
                    SqlBaseExpression param2 = ParseExpression(parentStatement, param2Node, source);
                    if (param2.ResultType != ResultTypes.String)
                    {
                        throw new SqlParserException(new SqlError(source,
                            param2Node.Position.Line,
                            param2Node.Position.Column,
                            $"Incorrect type of parameter ({param2Node.Value ?? "null"})"));
                    }

                    callParameters = new SqlBaseExpressionCollection();
                    callParameters.Add(param1);
                    callParameters.Add(param2);

                    break;
                case SqlParser.ID.VariableExactInOp:
                    inOp = SqlInExpression.OperationType.In;
                    break;
                case SqlParser.ID.VariableNotInOp:
                    inOp = SqlInExpression.OperationType.NotIn;
                    break;
                case SqlParser.ID.VariableExactNullOp:
                    unarOp = SqlUnarExpression.OperationType.IsNull;
                    break;
                case SqlParser.ID.VariableNotNullOp:
                    unarOp = SqlUnarExpression.OperationType.IsNotNull;
                    break;
                case SqlParser.ID.VariableGlobalParameter:
                    result = new GlobalParameter(parentStatement, fieldNode);
                    break;
                case SqlParser.ID.VariableLastResultCall:
                    result = new GetLastResult();
                    break;
                case SqlParser.ID.VariableRowsCountCall:
                    result = new GetRowsCount(parentStatement, fieldNode, source);
                    break;
                case SqlParser.ID.VariableGetRowCall:
                    result = new GetRow(parentStatement, fieldNode, source);
                    break;
                case SqlParser.ID.VariableGetFieldCall:
                    result = new GetField(parentStatement, fieldNode, source);
                    break;
                case SqlParser.ID.VariableNewRowsetCall:
                    result = new NewRowSet();
                    break;
                case SqlParser.ID.VariableNewRowCall:
                    result = new NewRow();
                    break;
            }
            if (funcName != null)
            {
                result = new SqlCallFuncExpression(funcResultType, funcName, callParameters);
            }
            if (constant != null)
            {
                if (opType == ResultTypes.Unknown && (string)constant == "NULL") constant = null;
                result = new SqlConstant(constant, opType);
            }
            if (binaryOp.HasValue)
            {
                SqlBaseExpression mLeftOperand = ParseExpression(parentStatement, fieldNode.Children[0], source);
                SqlBaseExpression mRightOperand = ParseExpression(parentStatement, fieldNode.Children[1], source);

                SqlConstant mConstant = SqlBinaryExpression.TryGetConstant(mLeftOperand, binaryOp.Value, mRightOperand);
                if (mConstant != null)
                    result = mConstant;
                else
                    result = new SqlBinaryExpression(mLeftOperand, binaryOp.Value, mRightOperand);
            }
            if (unarOp.HasValue)
            {
                SqlBaseExpression mOperand = ParseExpression(parentStatement, fieldNode.Children[0], source);

                SqlConstant mConstant = SqlUnarExpression.TryGetConstant(mOperand, unarOp.Value);
                if (mConstant != null)
                    result = mConstant;
                else
                    result = new SqlUnarExpression(mOperand, unarOp.Value);
            }
            if (inOp.HasValue)
            {
                result = new SqlInExpression(parentStatement, fieldNode.Children[0], inOp.Value, fieldNode.Children[1], source);
            }

            return result;
        }

    }
}
