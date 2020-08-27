using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using Gehtsoft.EF.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql
{
    public interface IStatementRunner<T>
    {
        object Run(T statement);
    }

    public abstract class StatementRunner<T> : IStatementRunner<T>
    {
        protected Dictionary<string, object> BindParams = new Dictionary<string, object>();

        public abstract object Run(T statement);

        protected abstract SqlStatement SqlStatement { get; }

        protected abstract QueryWithWhereBuilder MainBuilder { get; }

        protected abstract SqlDbConnection Connection { get; }

        protected abstract SqlCodeDomBuilder CodeDomBuilder { get; }

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
                if (SqlStatement.AliasEntrys.Exists(field.Name))
                {
                    return field.Name;
                }
                return MainBuilder.GetAlias(field.EntityDescriptor.TableDescriptor[field.Name]);
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
                        return Connection.GetLanguageSpecifics().GetAggFn(fn, MainBuilder.GetAlias(aggrFunc.Field.EntityDescriptor.TableDescriptor[aggrFunc.Field.Name]));
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
                }
                if (start.Contains("(")) end = ")";
                return $"{start}{GetStrExpression(unar.Operand, out isAggregate)}{end}";
            }
            else if (expression is SqlCallFuncExpression callFunc)
            {
                SqlFunctionId? funcId = null;
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
                }
                if (funcId.HasValue)
                {
                    List<string> pars = new List<string>();
                    foreach (SqlBaseExpression paramExpression in callFunc.Parameters)
                    {
                        bool isAggregateLocal;
                        pars.Add(GetStrExpression(paramExpression, out isAggregateLocal));
                        isAggregate = isAggregate || isAggregateLocal;
                    }
                    return $"({Connection.GetLanguageSpecifics().GetSqlFunction(funcId.Value, pars.ToArray())})";
                }
            }
            return null;
        }

        public enum ArifOp
        {
            Add,
            Minus,
            Divide,
            Multiply
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
}
