using Antlr4.Runtime.Tree;
using System;
using System.Globalization;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal static class SqlExpressionParser
    {
        internal static SqlBaseExpression ParseExpression(Statement parentStatement, SqlParser.ExprContext exprNode, string source)
        {
            switch (exprNode)
            {
                case SqlParser.PrimaryExprContext c:
                    return ParsePrimary(parentStatement, c.primary(), source);

                case SqlParser.UnarySignExprContext c:
                    {
                        var op = c.op.Text == "-" ? SqlUnaryExpression.OperationType.Minus : SqlUnaryExpression.OperationType.Plus;
                        return BuildUnary(parentStatement, c.expr(), op, source);
                    }
                case SqlParser.NotExprContext c:
                    return BuildUnary(parentStatement, c.expr(), SqlUnaryExpression.OperationType.Not, source);

                case SqlParser.NullExprContext c:
                    return BuildUnary(parentStatement, c.expr(),
                        c.not != null ? SqlUnaryExpression.OperationType.IsNotNull : SqlUnaryExpression.OperationType.IsNull, source);

                case SqlParser.MulExprContext c:
                    return BuildBinary(parentStatement, c.expr(0), c.expr(1),
                        c.op.Text == "*" ? SqlBinaryExpression.OperationType.Mult : SqlBinaryExpression.OperationType.Div, source);

                case SqlParser.AddExprContext c:
                    {
                        SqlBinaryExpression.OperationType op;
                        switch (c.op.Text)
                        {
                            case "+": op = SqlBinaryExpression.OperationType.Plus; break;
                            case "-": op = SqlBinaryExpression.OperationType.Minus; break;
                            default: op = SqlBinaryExpression.OperationType.Concat; break;
                        }
                        return BuildBinary(parentStatement, c.expr(0), c.expr(1), op, source);
                    }
                case SqlParser.RelExprContext c:
                    {
                        SqlBinaryExpression.OperationType op;
                        switch (c.op.Text)
                        {
                            case "=": op = SqlBinaryExpression.OperationType.Eq; break;
                            case "<>": op = SqlBinaryExpression.OperationType.Neq; break;
                            case ">": op = SqlBinaryExpression.OperationType.Gt; break;
                            case ">=": op = SqlBinaryExpression.OperationType.Ge; break;
                            case "<": op = SqlBinaryExpression.OperationType.Ls; break;
                            default: op = SqlBinaryExpression.OperationType.Le; break;
                        }
                        return BuildBinary(parentStatement, c.expr(0), c.expr(1), op, source);
                    }
                case SqlParser.AndExprContext c:
                    return BuildBinary(parentStatement, c.expr(0), c.expr(1), SqlBinaryExpression.OperationType.And, source);
                case SqlParser.OrExprContext c:
                    return BuildBinary(parentStatement, c.expr(0), c.expr(1), SqlBinaryExpression.OperationType.Or, source);

                case SqlParser.LikeExprContext c:
                    return BuildLike(parentStatement, c, source);

                case SqlParser.InExprContext c:
                    return new SqlInExpression(parentStatement, c.expr(),
                        c.not != null ? SqlInExpression.OperationType.NotIn : SqlInExpression.OperationType.In,
                        c.inPredicateValue(), source);

                case SqlParser.AssignExprContext c:
                    return new AssignExpression(parentStatement,
                        (GlobalParameter)ParseExpression(parentStatement, c.expr(0), source), c.expr(1), source);
            }
            throw new SqlParserException(new SqlError(source, exprNode.Line(), exprNode.Col(),
                $"Unexpected or incorrect expression ({exprNode.GetText()})"));
        }

        private static SqlBaseExpression ParsePrimary(Statement parentStatement, SqlParser.PrimaryContext primary, string source)
        {
            switch (primary.GetChild(0))
            {
                case SqlParser.ConstantContext c: return ParseConstant(c, source);
                case SqlParser.FieldContext c: return new SqlField(parentStatement, c, source);
                case SqlParser.GlobalParameterContext c: return new GlobalParameter(parentStatement, c);
                case SqlParser.FuncCallContext c: return ParseFuncCall(parentStatement, c, source);
                case SqlParser.AggrCallContext c: return ParseAggrCall(parentStatement, c, source);
                case SqlParser.SelectExprContext c: return new SqlSelectExpression(parentStatement, c, source);
                case ITerminalNode _: return ParseExpression(parentStatement, primary.expr(), source); // '(' expr ')'
            }
            throw new SqlParserException(new SqlError(source, primary.Line(), primary.Col(),
                $"Unexpected or incorrect expression ({primary.GetText()})"));
        }

        internal static SqlBaseExpression ParseConstant(SqlParser.ConstantContext ctx, string source)
        {
            switch (ctx)
            {
                case SqlParser.NullConstContext _:
                    return new SqlConstant(null, ResultTypes.Unknown);
                case SqlParser.BoolConstContext c:
                    return new SqlConstant(c.GetText() == "TRUE", ResultTypes.Boolean);
                case SqlParser.StringConstContext c:
                    return new SqlConstant(StripQuotes(c.GetText()), ResultTypes.String);
                case SqlParser.NumberConstContext c:
                    return c.INT() != null
                        ? new SqlConstant(int.Parse(c.GetText()), ResultTypes.Integer)
                        : new SqlConstant(double.Parse(c.GetText()), ResultTypes.Double);
                case SqlParser.DateConstContext c:
                    {
                        string v = StripQuotes(GetStringToken(c.STRINGDQ(), c.STRINGSQ()));
                        if (!DateTime.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                            throw new SqlParserException(new SqlError(source, c.Line(), c.Col(), $"Incorrect DateTime ({v})"));
                        return new SqlConstant(dt, ResultTypes.DateTime);
                    }
                case SqlParser.DatetimeConstContext c:
                    {
                        string v = StripQuotes(GetStringToken(c.STRINGDQ(), c.STRINGSQ()));
                        string[] formats = { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH", "yyyy-MM-dd" };
                        if (!DateTime.TryParseExact(v, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                            throw new SqlParserException(new SqlError(source, c.Line(), c.Col(), $"Incorrect DateTime ({v})"));
                        return new SqlConstant(dt, ResultTypes.DateTime);
                    }
            }
            throw new SqlParserException(new SqlError(source, ctx.Line(), ctx.Col(), $"Unexpected constant ({ctx.GetText()})"));
        }

        private static SqlBaseExpression ParseAggrCall(Statement parentStatement, SqlParser.AggrCallContext ctx, string source)
        {
            switch (ctx)
            {
                case SqlParser.AggrCountAllContext _:
                    return new SqlAggrFunc("COUNT", null, ResultTypes.Integer);
                case SqlParser.AggrFuncCallContext c:
                    {
                        string name = c.aggrFunc().GetText();
                        ResultTypes? resultType = name == "COUNT" ? ResultTypes.Integer : (ResultTypes?)null;
                        return new SqlAggrFunc(name, new SqlField(parentStatement, c.field(), source), resultType);
                    }
            }
            throw new SqlParserException(new SqlError(source, ctx.Line(), ctx.Col(), $"Unexpected aggregate ({ctx.GetText()})"));
        }

        private static SqlBaseExpression ParseFuncCall(Statement parentStatement, SqlParser.FuncCallContext ctx, string source)
        {
            switch (ctx.GetChild(0))
            {
                case SqlParser.MathFuncCallContext c:
                    {
                        var p = ParseExpression(parentStatement, c.expr(), source);
                        if (p.ResultType != ResultTypes.Integer && p.ResultType != ResultTypes.Double)
                            throw IncorrectParam(source, c.expr());
                        return new SqlCallFuncExpression(p.ResultType, c.name.Text, new SqlBaseExpressionCollection { p });
                    }
                case SqlParser.CastFuncCallContext c:
                    {
                        var p = ParseExpression(parentStatement, c.expr(), source);
                        ResultTypes rt;
                        switch (c.name.Text)
                        {
                            case "TOINT": rt = ResultTypes.Integer; break;
                            case "TODOUBLE": rt = ResultTypes.Double; break;
                            case "TODATE": rt = ResultTypes.DateTime; break;
                            case "TOTIMESTAMP": rt = ResultTypes.Integer; break;
                            default: rt = ResultTypes.String; break; // TOSTRING
                        }
                        return new SqlCallFuncExpression(rt, c.name.Text, new SqlBaseExpressionCollection { p });
                    }
                case SqlParser.StrFuncCallContext c:
                    {
                        var p = ParseExpression(parentStatement, c.expr(), source);
                        if (p.ResultType != ResultTypes.String)
                            throw IncorrectParam(source, c.expr());
                        return new SqlCallFuncExpression(ResultTypes.String, c.name.Text, new SqlBaseExpressionCollection { p });
                    }
                case SqlParser.TrimCallContext c:
                    {
                        string funcName = "TRIM";
                        if (c.trimSpecification() != null)
                        {
                            string spec = c.trimSpecification().GetText();
                            if (spec == "LEADING") funcName = "LTRIM";
                            else if (spec == "TRAILING") funcName = "RTRIM";
                        }
                        var p = ParseExpression(parentStatement, c.expr(), source);
                        if (p.ResultType != ResultTypes.String)
                            throw IncorrectParam(source, c.expr());
                        return new SqlCallFuncExpression(ResultTypes.String, funcName, new SqlBaseExpressionCollection { p });
                    }
                case SqlParser.BoolStrFuncCallContext c:
                    {
                        var p1 = ParseExpression(parentStatement, c.expr(0), source);
                        if (p1.ResultType != ResultTypes.String)
                            throw IncorrectParam(source, c.expr(0));
                        var p2 = ParseExpression(parentStatement, c.expr(1), source);
                        if (p2.ResultType != ResultTypes.String)
                            throw IncorrectParam(source, c.expr(1));
                        return new SqlCallFuncExpression(ResultTypes.Boolean, c.name.Text, new SqlBaseExpressionCollection { p1, p2 });
                    }
                case SqlParser.LastResultCallContext _: return new GetLastResult();
                case SqlParser.RowsCountCallContext c: return new GetRowsCount(parentStatement, c, source);
                case SqlParser.GetRowCallContext c: return new GetRow(parentStatement, c, source);
                case SqlParser.GetFieldCallContext c: return new GetField(parentStatement, c, source);
                case SqlParser.NewRowsetCallContext _: return new NewRowSet();
                case SqlParser.NewRowCallContext _: return new NewRow();
                case SqlParser.FetchCallContext c: return new Fetch(parentStatement, c, source);
            }
            throw new SqlParserException(new SqlError(source, ctx.Line(), ctx.Col(), $"Unexpected function call ({ctx.GetText()})"));
        }

        private static SqlBaseExpression BuildLike(Statement parentStatement, SqlParser.LikeExprContext c, string source)
        {
            string funcName = c.not != null ? "NOTLIKE" : "LIKE";
            var p1 = ParseExpression(parentStatement, c.expr(0), source);
            if (p1.ResultType != ResultTypes.String)
                throw IncorrectParam(source, c.expr(0));
            var p2 = ParseExpression(parentStatement, c.expr(1), source);
            if (p2.ResultType != ResultTypes.String)
                throw IncorrectParam(source, c.expr(1));
            return new SqlCallFuncExpression(ResultTypes.Boolean, funcName, new SqlBaseExpressionCollection { p1, p2 });
        }

        private static SqlBaseExpression BuildBinary(Statement parentStatement, SqlParser.ExprContext left, SqlParser.ExprContext right, SqlBinaryExpression.OperationType op, string source)
        {
            var l = ParseExpression(parentStatement, left, source);
            var r = ParseExpression(parentStatement, right, source);
            SqlConstant folded = SqlBinaryExpression.TryGetConstant(l, op, r);
            return folded ?? (SqlBaseExpression)new SqlBinaryExpression(l, op, r);
        }

        private static SqlBaseExpression BuildUnary(Statement parentStatement, SqlParser.ExprContext operand, SqlUnaryExpression.OperationType op, string source)
        {
            var o = ParseExpression(parentStatement, operand, source);
            SqlConstant folded = SqlUnaryExpression.TryGetConstant(o, op);
            return folded ?? (SqlBaseExpression)new SqlUnaryExpression(o, op);
        }

        private static SqlParserException IncorrectParam(string source, SqlParser.ExprContext ctx)
            => new SqlParserException(new SqlError(source, ctx.Line(), ctx.Col(), $"Incorrect type of parameter ({ctx.GetText()})"));

        private static string StripQuotes(string s) => s.Substring(1, s.Length - 2);

        private static string GetStringToken(ITerminalNode dq, ITerminalNode sq) => (dq ?? sq).GetText();
    }
}
