using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using Gehtsoft.EF.Test.SqlParser;

namespace Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder
{
    public static class AstNodeCommonExtensions
    {
        public static IEnumerable<IAstNode> Statements(this IAstNode tree) => tree.Select("/*");
        public static IAstNode Statement(this IAstNode tree, int index) => tree.Select("/*").Skip(index).FirstOrDefault();

        public static IAstNode DebugExpr(this IAstNode debugStatement) => debugStatement.SelectNode("/DEBUG_EXPR/*", 1);

        public static bool ExprIs(this IAstNode expr, string type) => expr != null && expr.Symbol == type;
        public static bool ExprIs(this IAstNode expr, params string[] type) => type.Any(t => expr.ExprIs(t));
        public static bool ExprIsField(this IAstNode expr) => ExprIs(expr, "FIELD");
        public static bool ExprFieldHasAlias(this IAstNode expr) => expr.Select("/IDENTIFIER").Count() > 1;
        public static string ExprFieldAlias(this IAstNode expr) => ExprFieldHasAlias(expr) ? expr.SelectNode("/IDENTIFIER", 1).Value : null;
        public static string ExprFieldName(this IAstNode expr) => ExprFieldHasAlias(expr) ? expr.SelectNode("/IDENTIFIER", 2).Value : expr.SelectNode("/IDENTIFIER", 1).Value;

        public static bool ExprIsParam(this IAstNode expr) => ExprIs(expr, "PARAM");
        public static string ExprParamName(this IAstNode expr) => expr.SelectNode("/IDENTIFIER", 1).Value;

        public static bool ExprIsTrue(this IAstNode expr) => ExprIs(expr, "BOOLEAN_TRUE");
        public static bool ExprIsFalse(this IAstNode expr) => ExprIs(expr, "BOOLEAN_FALSE");
        public static bool ExprIsBooleanConst(this IAstNode expr) => ExprIsTrue(expr) || ExprIsFalse(expr);
        public static bool ExprIsInt(this IAstNode expr) => ExprIs(expr, "INT");
        public static bool ExprIsReal(this IAstNode expr) => ExprIs(expr, "REAL");
        public static bool ExprIsString(this IAstNode expr) => ExprIs(expr, "STRINGDQ") || ExprIs(expr, "STRINGSQ");
        public static bool ExprIsNull(this IAstNode expr) => ExprIs(expr, "NULL");

        public static bool ExprIsConst(this IAstNode expr) => ExprIsNull(expr) || ExprIsString(expr) || ExprIsInt(expr) || ExprIsReal(expr) || ExprIsBooleanConst(expr);

        public static object ExprConstValue(this IAstNode expr)
        {
            if (ExprIsTrue(expr))
                return true;
            if (ExprIsFalse(expr))
                return false;
            if (ExprIsInt(expr))
                return Int32.Parse(expr.Value, CultureInfo.InvariantCulture);
            if (ExprIsReal(expr))
                return Double.Parse(expr.Value, CultureInfo.InvariantCulture);
            if (ExprIsString(expr))
            {
                var v = expr.Value;
                if (v.Length <= 2)
                    return "";
                return v.Substring(1, v.Length - 2);
            }
            throw new ArgumentException("The expression is not a constant", nameof(expr));
        }

        public static bool ExprIsCountAll(this IAstNode expr) => ExprIs(expr, "AGGR_COUNT_ALL");
        public static bool ExprIsAggFnCall(this IAstNode expr) => ExprIs(expr, "AGGR_FUNC");
        public static bool ExprIsMathFnCall(this IAstNode expr) => ExprIs(expr, "MATH_FUNC_CALL");
        public static bool ExprIsBoolFnCall(this IAstNode expr) => ExprIs(expr, "BOOL_STR_FUNC_CALL");
        public static bool ExprIsCastFnCall(this IAstNode expr) => ExprIs(expr, "CAST_FUNC_CALL");
        public static bool ExprIsStrFnCall(this IAstNode expr) => ExprIs(expr, "STR_FUNC_CALL");
        public static bool ExprIsTrimFnCall(this IAstNode expr) => ExprIs(expr, "TRIM_CALL");
        public static bool ExprIsDateCall(this IAstNode expr) => ExprIs(expr, "DATE_FUNC_CALL");
        public static bool ExprIsTwoArgCall(this IAstNode expr) => ExprIs(expr, "TWO_ARG_FUNC_CALL");

        public static bool ExprIsFnCall(this IAstNode expr) => ExprIsCountAll(expr) || ExprIsAggFnCall(expr) || ExprIsMathFnCall(expr) || ExprIsBoolFnCall(expr) || ExprIsCastFnCall(expr) || ExprIsStrFnCall(expr) || ExprIsTrimFnCall(expr) || ExprIsDateCall(expr) || ExprIsTwoArgCall(expr);

        public static string ExprFnCallName(this IAstNode expr)
        {
            if (ExprIsCountAll(expr))
                return "COUNT";
            if (ExprIsTrimFnCall(expr))
                return "TRIM";
            if (ExprIsAggFnCall(expr) || ExprIsStrFnCall(expr) || ExprIsMathFnCall(expr) || ExprIsCastFnCall(expr) || ExprIsBoolFnCall(expr) || ExprIsDateCall(expr) || ExprIsTwoArgCall(expr))
                return expr.SelectNode("/*", 1).Value;
            throw new ArgumentException("Expression is not a function call", nameof(expr));
        }

        public static int ExprFnCallArgCount(this IAstNode expr)
        {
            if (ExprIsCountAll(expr))
                return 0;
            if (ExprIsTrimFnCall(expr))
                return 1;
            if (ExprIsAggFnCall(expr) || ExprIsStrFnCall(expr) || ExprIsMathFnCall(expr) || ExprIsCastFnCall(expr) || ExprIsBoolFnCall(expr) || ExprIsDateCall(expr) || ExprIsTwoArgCall(expr))
                return expr.Select("/*").Count() - 1;
            throw new ArgumentException("Expression is not a function call", nameof(expr));
        }

        public static IAstNode ExprFnCallArg(this IAstNode expr, int index = 0)
        {
            if (ExprIsCountAll(expr))
                return null;
            if (ExprIsTrimFnCall(expr))
                return expr.SelectNode("/*", 1);
            if (ExprIsAggFnCall(expr) || ExprIsStrFnCall(expr) || ExprIsMathFnCall(expr) || ExprIsCastFnCall(expr) || ExprIsBoolFnCall(expr) || ExprIsDateCall(expr) || ExprIsTwoArgCall(expr))
                return expr.SelectNode("/*", index + 2);
            throw new ArgumentException("Expression is not a function call", nameof(expr));
        }

        private readonly static string[] OPS = new string[] { "MINUS_OP", "PLUS_OP", "MUL_OP", "DIV_OP", "CONCAT_OP", "EQ_OP", "NEQ_OP", "GT_OP", "GE_OP", "LT_OP", "LE_OP", "LIKE_OP", "NOT_LIKE_OP", "IN_OP", "NOT_IN_OP", "EXISTS_OP", "NOT_EXISTS_OP", "NULL_OP", "NOT_NULL_OP", "EQ_OP", "NEQ_OP", "GT_OP", "GE_OP", "LT_OP", "LE_OP", "NOT_OP", "AND_OP", "OR_OP" };

        public static bool ExprIsOp(this IAstNode expr) => ExprIs(expr, OPS);

        public static string ExprOp(this IAstNode expr) => ExprIsOp(expr) ? expr.Symbol : null;

        public static int ExprOpArgCount(this IAstNode expr)
        {
            if (expr.ExprIsOp())
                return expr.Select("/*").Count();
            throw new ArgumentException("The node is not an operator", nameof(expr));
        }

        public static IAstNode ExprOpArg(this IAstNode expr, int index = 0)
        {
            if (expr.ExprIsOp())
                return expr.SelectNode("/*", index + 1);
            throw new ArgumentException("The node is not an operator", nameof(expr));
        }
    }
}

