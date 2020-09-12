﻿using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using Gehtsoft.EF.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql
{
    public interface IStatementRunner<T>
    {
        object Run(T statement);
    }

    public interface IBindParamsOwner
    {
        Dictionary<string, object> BindParams { get; }
    }

    public abstract class StatementRunner<T> : IStatementRunner<T>
    {
        public abstract object Run(T statement);

        protected abstract SqlDbConnection Connection { get; }

        protected abstract SqlCodeDomBuilder CodeDomBuilder { get; }

        protected SqlConstant CalculateExpression(SqlBaseExpression expression)
        {
            if (expression is SqlField field)
            {
                return null;
            }
            else if (expression is SqlAggrFunc aggrFunc)
            {
                return null;
            }
            else if (expression is SqlBinaryExpression binaryExpression)
            {
                SqlBaseExpression leftOperand = CalculateExpression(binaryExpression.LeftOperand);
                SqlBaseExpression rightOperand = CalculateExpression(binaryExpression.RightOperand);

                if (leftOperand == null || rightOperand == null)
                    return null;

                return SqlBinaryExpression.TryGetConstant(leftOperand, binaryExpression.Operation, rightOperand);
            }
            else if (expression is SqlConstant constant)
            {
                return constant;
            }
            else if (expression is GlobalParameter globalParameter)
            {
                return CalculateExpression(globalParameter.InnerExpression);
            }
            else if (expression is GetLastResult)
            {
                return new SqlConstant(CodeDomBuilder.LastStatementResult, ResultTypes.RowSet);
            }
            else if (expression is GetRowsCount getRowsCount)
            {
                SqlConstant param = CalculateExpression(getRowsCount.Parameter);
                if (param == null)
                    return null;

                List<object> array = param.Value as List<object>; ;
                return new SqlConstant(array.Count, ResultTypes.Integer);
            }
            else if (expression is SqlUnarExpression unar)
            {
                SqlBaseExpression operand = CalculateExpression(unar.Operand);

                if (operand == null || !(operand is SqlConstant))
                    return null;

                return SqlUnarExpression.TryGetConstant(operand, unar.Operation);
            }
            else if (expression is SqlCallFuncExpression callFunc)
            {
                List<SqlConstant> pars = new List<SqlConstant>();
                foreach (SqlBaseExpression expr in callFunc.Parameters)
                {
                    SqlBaseExpression curr = CalculateExpression(expr);
                    if (curr is SqlConstant cnst)
                    {
                        pars.Add(cnst);
                    }
                    else
                    {
                        return null;
                    }
                }

                ResultTypes resultType = ResultTypes.Unknown;
                object value = null;

                try
                {
                    switch (callFunc.Name)
                    {
                        case "TRIM":
                            resultType = ResultTypes.String;
                            value = ((string)pars[0].Value).Trim();
                            break;
                        case "LTRIM":
                            resultType = ResultTypes.String;
                            value = ((string)pars[0].Value).TrimStart();
                            break;
                        case "RTRIM":
                            resultType = ResultTypes.String;
                            value = ((string)pars[0].Value).TrimEnd();
                            break;
                        case "UPPER":
                            resultType = ResultTypes.String;
                            value = ((string)pars[0].Value).ToUpper();
                            break;
                        case "LOWER":
                            resultType = ResultTypes.String;
                            value = ((string)pars[0].Value).ToLower();
                            break;
                        case "TOSTRING":
                            resultType = ResultTypes.String;
                            value = ((string)pars[0].Value).ToString();
                            break;
                        case "TOINTEGER":
                            int intRes;
                            if (int.TryParse((string)pars[0].Value, out intRes))
                            {
                                resultType = ResultTypes.Integer;
                                value = intRes;
                            }
                            break;
                        case "TODOUBLE":
                            double doubleRes;
                            if (double.TryParse((string)pars[0].Value, out doubleRes))
                            {
                                resultType = ResultTypes.Double;
                                value = doubleRes;
                            }
                            break;
                        case "TODATE":
                            DateTime? dtt = tryParseDateTime((string)pars[0].Value);
                            if (dtt.HasValue)
                            {
                                resultType = ResultTypes.DateTime;
                                value = dtt.Value;
                            }
                            break;
                        case "TOTIMESTAMP":
                            DateTime? dtt1 = tryParseDateTime((string)pars[0].Value);
                            if (dtt1.HasValue)
                            {
                                resultType = ResultTypes.Integer;
                                value = unixTimeStampUTC(dtt1.Value);
                            }
                            break;
                        case "ABS":
                            if (pars[0].ResultType == ResultTypes.Integer)
                            {
                                resultType = ResultTypes.Integer;
                                value = Math.Abs((int)pars[0].Value);
                            }
                            else if (pars[0].ResultType == ResultTypes.Double)
                            {
                                resultType = ResultTypes.Double;
                                value = Math.Abs((double)pars[0].Value);
                            }
                            else
                            {
                                return null;
                            }
                            break;
                        case "LIKE":
                            resultType = ResultTypes.Boolean;
                            value = ((string)pars[0].Value).Like(((string)pars[1].Value));
                            break;
                        case "NOTLIKE":
                            resultType = ResultTypes.Boolean;
                            value = !((string)pars[0].Value).Like(((string)pars[1].Value));
                            break;
                        case "STARTSWITH":
                            resultType = ResultTypes.Boolean;
                            value = !((string)pars[0].Value).StartsWith(((string)pars[1].Value));
                            break;
                        case "ENDSWITH":
                            resultType = ResultTypes.Boolean;
                            value = !((string)pars[0].Value).EndsWith(((string)pars[1].Value));
                            break;
                        case "CONTAINS":
                            resultType = ResultTypes.Boolean;
                            value = !((string)pars[0].Value).Contains(((string)pars[1].Value));
                            break;
                    }
                }
                catch
                {
                    return null;
                }
                return new SqlConstant(value, resultType);
            }
            else if (expression is SqlInExpression inExpression)
            {
                bool inExpressionResult = false;
                SqlBaseExpression leftOperand = CalculateExpression(inExpression.LeftOperand);
                if (leftOperand == null)
                    return null;

                SqlBaseExpression rightOperand = null;
                if (inExpression.RightOperandAsList != null)
                {
                    foreach (SqlBaseExpression expr in inExpression.RightOperandAsList)
                    {
                        rightOperand = CalculateExpression(inExpression.LeftOperand);
                        if (rightOperand == null)
                            return null;
                        if(((SqlConstant)leftOperand).Equals((SqlConstant)rightOperand))
                        {
                            inExpressionResult = true;
                            break;
                        }
                    }
                }
                else if (inExpression.RightOperandAsSelect != null)
                {
                    SelectRunner runner = new SelectRunner(CodeDomBuilder, Connection);
                    List<object> selectResult = runner.Run(inExpression.RightOperandAsSelect) as List<object>;
                    foreach(object recordObj in selectResult)
                    {
                        Dictionary<string, object> record = recordObj as Dictionary<string, object>;
                        if(((SqlConstant)leftOperand).Value.Equals(record[record.Keys.First()]))
                        {
                            inExpressionResult = true;
                            break;
                        }
                    }
                }
                return new SqlConstant(inExpressionResult, ResultTypes.Boolean);
            }
            else if (expression is SqlSelectExpression selectExpression)
            {
                SelectRunner runner = new SelectRunner(CodeDomBuilder, Connection);
                List<object> selectResult = runner.Run(selectExpression.SelectStatement) as List<object>;
                if (selectResult.Count > 0)
                {
                    object recordObj = selectResult[0];
                    Dictionary<string, object> record = recordObj as Dictionary<string, object>;
                    return new SqlConstant(record[record.Keys.First()], selectExpression.ResultType);
                }
                else
                {
                    return new SqlConstant(null, selectExpression.ResultType);
                }
            }
            return null;
        }

        private DateTime? tryParseDateTime(string strDateTime)
        {
            DateTime dtt;
            if (!DateTime.TryParseExact(strDateTime,
                "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dtt))
            {
                if (!DateTime.TryParseExact(strDateTime,
                    "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out dtt))
                {
                    if (!DateTime.TryParseExact(strDateTime,
                        "yyyy-MM-dd HH", CultureInfo.InvariantCulture, DateTimeStyles.None, out dtt))
                    {
                        if (!DateTime.TryParseExact(strDateTime,
                            "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dtt))
                        {
                            return null;
                        }
                    }
                }
            }
            return dtt;
        }

        public int unixTimeStampUTC(DateTime currentTime)
        {
            int unixTimeStamp;
            DateTime zuluTime = currentTime.ToUniversalTime();
            DateTime unixEpoch = new DateTime(1970, 1, 1);
            unixTimeStamp = (Int32)(zuluTime.Subtract(unixEpoch)).TotalSeconds;
            return unixTimeStamp;
        }

        public enum ArifOp
        {
            Add,
            Minus,
            Divide,
            Multiply
        }
    }

    public abstract class SqlStatementRunner<T> : StatementRunner<T>, IBindParamsOwner
    {
        protected IBindParamsOwner BindParamsOwner { get; set; } = null;

        private Dictionary<string, object> mBindParams = new Dictionary<string, object>();

        public Dictionary<string, object> BindParams
        {
            get
            {
                if (BindParamsOwner == null)
                    return mBindParams;
                return BindParamsOwner.BindParams;
            }
        }

        public abstract AQueryBuilder GetQueryBuilder(T statement);

        protected abstract SqlStatement SqlStatement { get; }

        protected abstract QueryWithWhereBuilder MainBuilder { get; }

        protected string GetStrExpression(SqlBaseExpression expression)
        {
            bool isAggregate;
            return GetStrExpression(expression, out isAggregate);
        }

        protected string GetStrExpression(SqlBaseExpression expression, out bool isAggregate)
        {
            isAggregate = false;
            if (expression is SqlField field)
            {
                string result = null;
                if (field.EntityDescriptor != null)
                {
                    try
                    {
                        result = GetAlias(field.EntityDescriptor.TableDescriptor[field.FieldName]);
                    }
                    catch { }
                }
                if (result == null)
                {
                    if (SqlStatement.AliasEntrys.Exists(field.Name))
                    {
                        result = field.Name;
                    }
                    else
                    {
                        throw new SqlParserException(new SqlError(null, 0, 0, $"Unknown operand '{field.Name}'"));
                    }
                }
                return result;
            }
            else if (expression is SqlAggrFunc aggrFunc)
            {
                isAggregate = true;
                if (aggrFunc.Name == "COUNT" && aggrFunc.Field == null) // COUNT(*)
                {
                    return Connection.GetLanguageSpecifics().GetAggFn(AggFn.Count, null);
                }
                else
                {
                    AggFn fn = AggFn.None;
                    switch (aggrFunc.Name)
                    {
                        case "COUNT":
                            fn = AggFn.Count;
                            break;
                        case "MAX":
                            fn = AggFn.Max;
                            break;
                        case "MIN":
                            fn = AggFn.Min;
                            break;
                        case "AVG":
                            fn = AggFn.Avg;
                            break;
                        case "SUM":
                            fn = AggFn.Sum;
                            break;
                    }
                    if (fn != AggFn.None)
                    {
                        return Connection.GetLanguageSpecifics().GetAggFn(fn, GetAlias(aggrFunc.Field.EntityDescriptor.TableDescriptor[aggrFunc.Field.Name]));
                    }
                }
            }
            else if (expression is SqlBinaryExpression binaryExpression)
            {
                bool isAggregateLeft;
                bool isAggregateRight;
                string leftOperand = GetStrExpression(binaryExpression.LeftOperand, out isAggregateLeft);
                string rightOperand = GetStrExpression(binaryExpression.RightOperand, out isAggregateRight);
                isAggregate = isAggregateLeft || isAggregateRight;

                CmpOp? op = null;
                LogOp? logOp = null;
                ArifOp? arifOp = null;
                switch (binaryExpression.Operation)
                {
                    case SqlBinaryExpression.OperationType.Eq:
                        op = CmpOp.Eq;
                        break;
                    case SqlBinaryExpression.OperationType.Neq:
                        op = CmpOp.Neq;
                        break;
                    case SqlBinaryExpression.OperationType.Gt:
                        op = CmpOp.Gt;
                        break;
                    case SqlBinaryExpression.OperationType.Ge:
                        op = CmpOp.Ge;
                        break;
                    case SqlBinaryExpression.OperationType.Ls:
                        op = CmpOp.Ls;
                        break;
                    case SqlBinaryExpression.OperationType.Le:
                        op = CmpOp.Le;
                        break;
                    case SqlBinaryExpression.OperationType.Or:
                        logOp = LogOp.Or;
                        break;
                    case SqlBinaryExpression.OperationType.And:
                        logOp = LogOp.And;
                        break;
                    case SqlBinaryExpression.OperationType.Plus:
                        arifOp = ArifOp.Add;
                        break;
                    case SqlBinaryExpression.OperationType.Minus:
                        arifOp = ArifOp.Minus;
                        break;
                    case SqlBinaryExpression.OperationType.Div:
                        arifOp = ArifOp.Divide;
                        break;
                    case SqlBinaryExpression.OperationType.Mult:
                        arifOp = ArifOp.Multiply;
                        break;
                    case SqlBinaryExpression.OperationType.Concat:
                        List<string> pars = new List<string>() { leftOperand, rightOperand };
                        return $"({Connection.GetLanguageSpecifics().GetSqlFunction(SqlFunctionId.Concat, pars.ToArray())})";
                    default:
                        throw new SqlParserException(new SqlError(null, 0, 0, $"Unknown operation"));
                }

                if (op.HasValue)
                    return $"({Connection.GetLanguageSpecifics().GetOp(op.Value, leftOperand, rightOperand)})";
                else if (logOp.HasValue)
                    return $"({leftOperand}{Connection.GetLanguageSpecifics().GetLogOp(logOp.Value)}{rightOperand})";
                else if (arifOp.HasValue)
                    return $"({GetArifOp(arifOp.Value, leftOperand, rightOperand)})";
            }
            else if (expression is SqlConstant constant)
            {
                string paramName = $"$param${BindParams.Count}";
                BindParams.Add(paramName, constant.Value);
                return GetParameter(paramName);
            }
            else if (expression is GlobalParameter globalParameter)
            {
                return GetStrExpression(globalParameter.InnerExpression, out isAggregate);
            }
            else if (expression is GetLastResult getLastResult)
            {
                return GetStrExpression(CalculateExpression(getLastResult), out isAggregate);
            }
            else if (expression is GetRowsCount getRowsCount)
            {
                return GetStrExpression(CalculateExpression(getRowsCount), out isAggregate);
            }

            else if (expression is SqlUnarExpression unar)
            {
                string start = string.Empty;
                string end = string.Empty;
                switch (unar.Operation)
                {
                    case SqlUnarExpression.OperationType.Minus:
                        start = " -(";
                        break;
                    case SqlUnarExpression.OperationType.Plus:
                        start = " -(";
                        break;
                    case SqlUnarExpression.OperationType.Not:
                        start = Connection.GetLanguageSpecifics().GetLogOp(LogOp.Not);
                        break;
                    case SqlUnarExpression.OperationType.IsNull:
                        return $"({Connection.GetLanguageSpecifics().GetOp(CmpOp.IsNull, GetStrExpression(unar.Operand, out isAggregate), null)})";
                    case SqlUnarExpression.OperationType.IsNotNull:
                        return $"({Connection.GetLanguageSpecifics().GetOp(CmpOp.NotNull, GetStrExpression(unar.Operand, out isAggregate), null)})";
                }
                if (start.Contains("(")) end = ")";
                return $"{start}{GetStrExpression(unar.Operand, out isAggregate)}{end}";
            }
            else if (expression is SqlCallFuncExpression callFunc)
            {
                SqlFunctionId? funcId = null;
                bool isNot = false;
                SqlBaseExpressionCollection collection = null;
                switch (callFunc.Name)
                {
                    case "TRIM":
                        funcId = SqlFunctionId.Trim;
                        break;
                    case "LTRIM":
                        funcId = SqlFunctionId.TrimLeft;
                        break;
                    case "RTRIM":
                        funcId = SqlFunctionId.TrimRight;
                        break;
                    case "UPPER":
                        funcId = SqlFunctionId.Upper;
                        break;
                    case "LOWER":
                        funcId = SqlFunctionId.Lower;
                        break;
                    case "TOSTRING":
                        funcId = SqlFunctionId.ToString;
                        break;
                    case "TOINTEGER":
                        funcId = SqlFunctionId.ToInteger;
                        break;
                    case "TODOUBLE":
                        funcId = SqlFunctionId.ToDouble;
                        break;
                    case "TODATE":
                        funcId = SqlFunctionId.ToDate;
                        break;
                    case "TOTIMESTAMP":
                        funcId = SqlFunctionId.ToTimestamp;
                        break;
                    case "ABS":
                        funcId = SqlFunctionId.Abs;
                        break;
                    case "LIKE":
                        funcId = SqlFunctionId.Like;
                        break;
                    case "NOTLIKE":
                        funcId = SqlFunctionId.Like;
                        isNot = true;
                        break;
                    case "STARTSWITH":
                        funcId = SqlFunctionId.Like;
                        SqlBaseExpression par2 = callFunc.Parameters[1];
                        collection = new SqlBaseExpressionCollection();
                        collection.Add(callFunc.Parameters[0]);

                        SqlBaseExpression newpar2 = new SqlBinaryExpression(par2,
                            SqlBinaryExpression.OperationType.Concat,
                            new SqlConstant("%", SqlBaseExpression.ResultTypes.String)
                        );
                        collection.Add(newpar2);
                        break;
                    case "ENDSWITH":
                        funcId = SqlFunctionId.Like;
                        SqlBaseExpression epar2 = callFunc.Parameters[1];
                        collection = new SqlBaseExpressionCollection();
                        collection.Add(callFunc.Parameters[0]);

                        SqlBaseExpression enewpar2 = new SqlBinaryExpression(new SqlConstant("%", SqlBaseExpression.ResultTypes.String),
                            SqlBinaryExpression.OperationType.Concat,
                            epar2
                        );
                        collection.Add(enewpar2);
                        break;
                    case "CONTAINS":
                        funcId = SqlFunctionId.Like;
                        SqlBaseExpression cpar2 = callFunc.Parameters[1];
                        collection = new SqlBaseExpressionCollection();
                        collection.Add(callFunc.Parameters[0]);

                        SqlBaseExpression cnewpar2 = new SqlBinaryExpression(new SqlConstant("%", SqlBaseExpression.ResultTypes.String),
                            SqlBinaryExpression.OperationType.Concat,
                            new SqlBinaryExpression(cpar2,
                                SqlBinaryExpression.OperationType.Concat,
                                new SqlConstant("%", SqlBaseExpression.ResultTypes.String)
                            )
                        );
                        collection.Add(cnewpar2);
                        break;
                }
                if (collection == null)
                {
                    collection = callFunc.Parameters;
                }
                if (funcId.HasValue)
                {
                    List<string> pars = new List<string>();
                    foreach (SqlBaseExpression paramExpression in collection)
                    {
                        bool isAggregateLocal;
                        pars.Add(GetStrExpression(paramExpression, out isAggregateLocal));
                        isAggregate = isAggregate || isAggregateLocal;
                    }
                    string retval = $"({Connection.GetLanguageSpecifics().GetSqlFunction(funcId.Value, pars.ToArray())})";
                    if (isNot)
                    {
                        string start = Connection.GetLanguageSpecifics().GetLogOp(LogOp.Not);
                        retval = $"{start}{retval}";
                    }
                    return retval;
                }
            }
            else if (expression is SqlInExpression inExpression)
            {
                bool isAggregateLeft;
                bool isAggregateRight = false;
                string leftOperand = GetStrExpression(inExpression.LeftOperand, out isAggregateLeft);
                string rightOperand = null;
                if (inExpression.RightOperandAsList != null)
                {
                    StringBuilder rightBuilder = new StringBuilder();
                    foreach (SqlBaseExpression expr in inExpression.RightOperandAsList)
                    {
                        bool isAggregateRightLocal;
                        rightBuilder.Append(rightBuilder.Length == 0 ? "(" : ",");
                        rightBuilder.Append(GetStrExpression(expr, out isAggregateRightLocal));
                        isAggregateRight = isAggregateRight || isAggregateRightLocal;
                    }
                    rightBuilder.Append(")");
                    rightOperand = rightBuilder.ToString();
                }
                else if (inExpression.RightOperandAsSelect != null)
                {
                    SelectRunner runner = new SelectRunner(CodeDomBuilder, Connection, this);
                    AQueryBuilder builder = runner.GetQueryBuilder(inExpression.RightOperandAsSelect);
                    builder.PrepareQuery();
                    rightOperand = $"({builder.Query})";
                }

                isAggregate = isAggregateLeft || isAggregateRight;

                CmpOp op = CmpOp.In;
                if (inExpression.Operation == SqlInExpression.OperationType.NotIn)
                {
                    op = CmpOp.NotIn;
                }

                return $"({Connection.GetLanguageSpecifics().GetOp(op, leftOperand, rightOperand)})";
            }
            else if (expression is SqlSelectExpression selectExpression)
            {
                SelectRunner runner = new SelectRunner(CodeDomBuilder, Connection, this);
                AQueryBuilder builder = runner.GetQueryBuilder(selectExpression.SelectStatement);
                builder.PrepareQuery();
                return $"({builder.Query})";
            }
            return null;
        }

        internal string GetAlias(TableDescriptor.ColumnInfo info, QueryBuilderEntity entity = null)
        {
            if (MainBuilder != null)
                return MainBuilder.GetAlias(info, entity);

            return $"{info.Name}";
        }

        internal string GetArifOp(ArifOp op, string leftSide, string rightSide)
        {
            switch (op)
            {
                case ArifOp.Add:
                    return $"{leftSide} + {rightSide}";

                case ArifOp.Minus:
                    return $"{leftSide} - {rightSide}";

                case ArifOp.Multiply:
                    return $"{leftSide} * {rightSide}";

                case ArifOp.Divide:
                    return $"{leftSide} / {rightSide}";
                default:
                    throw new SqlParserException(new SqlError(null, 0, 0, $"Unknown arifmetic operation"));
            }
        }

        protected string GetParameter(string parameterName)
        {
            if (parameterName == null)
                return null;

            string prefix = Connection.GetLanguageSpecifics().ParameterInQueryPrefix;
            if (!string.IsNullOrEmpty(prefix) && !parameterName.StartsWith(prefix))
                parameterName = prefix + parameterName;
            return parameterName;
        }

        protected void ApplyBindParams(SqlDbQuery query)
        {
            foreach (KeyValuePair<string, object> pair in BindParams)
            {
                if (pair.Value == null)
                {
                    query.BindParam(pair.Key, DbType.String, null);
                }
                else
                {
                    Type tttt = pair.Value.GetType();
                    if (pair.Value is int intValue)
                        query.BindParam(pair.Key, intValue);
                    else if (pair.Value is double doubleValue)
                        query.BindParam(pair.Key, doubleValue);
                    else if (pair.Value is bool boolValue)
                        query.BindParam(pair.Key, boolValue);
                    else if (pair.Value is DateTime dateTimeValue)
                        query.BindParam(pair.Key, dateTimeValue);
                    else if (pair.Value is DateTimeOffset dateTimeOffsetValue)
                        query.BindParam(pair.Key, dateTimeOffsetValue.LocalDateTime);
                    else
                        query.BindParam(pair.Key, pair.Value.ToString());
                }
            }
        }

        protected TableDescriptor FindTableDescriptor(string entityName)
        {
            Type entityType = CodeDomBuilder.EntityByName(entityName);
            if (entityType == null)
                throw new SqlParserException(new SqlError(null, 0, 0, $"Not found entity with name '{entityName}'"));
            return AllEntities.Inst[entityType].TableDescriptor;
        }

        protected DbType GetDbType(Type propType)
        {
            DbType result = DbType.String;

            if (propType == typeof(string))
            {
                result = DbType.String;
            }
            else if (propType == typeof(Guid))
            {
                result = DbType.Guid;
            }
            else if (propType == typeof(bool))
            {
                result = DbType.Boolean;
            }
            else if (propType == typeof(int))
            {
                result = DbType.Int32;
            }
            else if (propType == typeof(double))
            {
                result = DbType.Double;
            }
            else if (propType == typeof(DateTime))
            {
                result = DbType.DateTime;
            }

            return result;
        }

    }
    public static class MyStringExtensions
    {
        public static bool Like(this string toSearch, string toFind)
        {
            return new Regex(@"\A" + new Regex(@"\.|\$|\^|\{|\[|\(|\||\)|\*|\+|\?|\\").Replace(toFind, ch => @"\" + ch).Replace('_', '.').Replace("%", ".*") + @"\z", RegexOptions.Singleline).IsMatch(toSearch);
        }
    }
}
