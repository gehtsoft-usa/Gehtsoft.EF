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
    internal class SelectRunner : IStatementRunner<SqlSelectStatement>
    {
        private SqlCodeDomBuilder mBuilder;
        private SqlDbConnection mConnection;
        private readonly ISqlDbConnectionFactory mConnectionFactory;
        private MySelectQueryBuilder mMainBuilder = null;
        private EntityDescriptor mMainEntityDescriptor = null;
        private Dictionary<string, object> mBindParams = new Dictionary<string, object>();

        internal SelectRunner(SqlCodeDomBuilder builder, ISqlDbConnectionFactory connectionFactory)
        {
            mBuilder = builder;
            mConnectionFactory = connectionFactory;
        }

        public object Run(SqlSelectStatement select)
        {
            List<object> result = new List<object>();
            mConnection = mConnectionFactory.GetConnection();
            try
            {
                processFrom(select.FromClause);
                processSelectList(select.SelectList);

                if (select.SetQuantifier == "DISTINCT")
                    mMainBuilder.Distinct = true;

                using (SqlDbQuery query = mConnection.GetQuery(mMainBuilder))
                {
                    bindParams(query);

                    query.ExecuteReader();
                    while (query.ReadNext())
                    {
                        object o = bindRecord(query, select);
                        result.Add(o);
                    }
                }
            }
            finally
            {
                if (mConnectionFactory.NeedDispose)
                    mConnection.Dispose();
            }
            return result;
        }

        private void processSelectList(SqlSelectList selectList)
        {
            if (!selectList.All)
            {
                foreach (SqlExpressionAlias item in selectList.FieldAliasCollection)
                {
                    bool isAggregate;
                    string sExpr = getStrExpression(item.Expression, out isAggregate);
                    if (sExpr == null)
                        throw new SqlParserException(new SqlError(null, 0, 0, $"Unknown expression"));
                    mMainBuilder.AddExpressionToResultset(sExpr, getDbType(item.Expression.RealType), isAggregate, item.Alias);
                }
            }
        }

        private string getStrExpression(SqlBaseExpression expression, out bool isAggregate)
        {
            isAggregate = false;
            if (expression is SqlField field)
            {
                return mMainBuilder.GetAlias(field.EntityDescriptor.TableDescriptor[field.Name]);
            }
            else if (expression is SqlAggrFunc aggrFunc)
            {
                isAggregate = true;
                if (aggrFunc.Name == "COUNT" && aggrFunc.Field == null) // COUNT(*)
                {
                    return mConnection.GetLanguageSpecifics().GetAggFn(AggFn.Count, null);
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
                        return mConnection.GetLanguageSpecifics().GetAggFn(fn, mMainBuilder.GetAlias(aggrFunc.Field.EntityDescriptor.TableDescriptor[aggrFunc.Field.Name]));
                    }
                }
            }
            else if (expression is SqlBinaryExpression binaryExpression)
            {
                bool isAggregateLeft;
                bool isAggregateRight;
                string leftOperand = getStrExpression(binaryExpression.LeftOperand, out isAggregateLeft);
                string rightOperand = getStrExpression(binaryExpression.RightOperand, out isAggregateRight);
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
                        List<string> pars = new List<string>() { leftOperand , rightOperand};
                        return $"({mConnection.GetLanguageSpecifics().GetSqlFunction(SqlFunctionId.Concat, pars.ToArray())})";
                    default:
                        throw new SqlParserException(new SqlError(null, 0, 0, $"Unknown operation"));
                }

                if (op.HasValue)
                    return $"({mConnection.GetLanguageSpecifics().GetOp(op.Value, leftOperand, rightOperand)})";
                else if (logOp.HasValue)
                    return $"({leftOperand}{mConnection.GetLanguageSpecifics().GetLogOp(logOp.Value)}{rightOperand})";
                else if (arifOp.HasValue)
                    return $"({GetArifOp(arifOp.Value, leftOperand, rightOperand)})";
            }
            else if (expression is SqlConstant constant)
            {
                string paramName = $"$param${mBindParams.Count}";
                mBindParams.Add(paramName, constant.Value);
                return getParameter(paramName);
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
                        start = mConnection.GetLanguageSpecifics().GetLogOp(LogOp.Not);
                        break;
                }
                if (start.Contains("(")) end = ")";
                return $"{start}{getStrExpression(unar.Operand, out isAggregate)}{end}";
            }
            else if (expression is SqlCallFuncExpression callFunc)
            {
                SqlFunctionId? funcId = null;
                switch(callFunc.Name)
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
                if(funcId.HasValue)
                {
                    List<string> pars = new List<string>();
                    foreach(SqlBaseExpression paramExpression in callFunc.Parameters)
                    {
                        bool isAggregateLocal;
                        pars.Add(getStrExpression(paramExpression, out isAggregateLocal));
                        isAggregate = isAggregate || isAggregateLocal;
                    }
                    return $"({mConnection.GetLanguageSpecifics().GetSqlFunction(funcId.Value, pars.ToArray())})";
                }
            }
            return null;
        }
        private DbType getDbType(Type propType)
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

        private void diveTableSpecification(SqlTableSpecification table)
        {
            if (table is SqlPrimaryTable primaryTable)
            {
                if (mMainBuilder == null)
                {
                    mMainBuilder = createBuilder(primaryTable.TableName, out mMainEntityDescriptor);
                }
                else
                {
                    mMainBuilder.AddTable(findTableDescriptor(primaryTable.TableName), false);
                }
            }
            if (table is SqlQualifiedJoinedTable joinedTable)
            {
                diveTableSpecification(joinedTable.LeftTable);

                TableJoinType joinType = TableJoinType.None;
                switch(joinedTable.JoinType)
                {
                    case "INNER":
                        joinType = TableJoinType.Inner;
                        break;
                    case "LEFT":
                        joinType = TableJoinType.Left;
                        break;
                    case "RIGHT":
                        joinType = TableJoinType.Right;
                        break;
                    case "FULL":
                        joinType = TableJoinType.Outer;
                        break;
                }
                bool isAggregate;
                QueryBuilderEntity builderEntity = mMainBuilder.AddTable(findTableDescriptor(joinedTable.RightTable.TableName), joinType);
                builderEntity.On.Add(LogOp.And, getStrExpression(joinedTable.JoinCondition, out isAggregate));
            }

        }

        private void processFrom(SqlFromClause fromClause)
        {
            foreach (SqlTableSpecification table in fromClause.TableCollection)
            {
                diveTableSpecification(table);
            }
        }

        private MySelectQueryBuilder createBuilder(string entityName, out EntityDescriptor entityDescriptor)
        {
            Type entityType = mBuilder.EntityByName(entityName);
            if (entityType == null)
                throw new SqlParserException(new SqlError(null, 0, 0, $"Not found entity with name '{entityName}'"));
            entityDescriptor = AllEntities.Inst[entityType];
            return new MySelectQueryBuilder(mConnection.GetLanguageSpecifics(), entityDescriptor.TableDescriptor);
        }

        private TableDescriptor findTableDescriptor(string entityName)
        {
            Type entityType = mBuilder.EntityByName(entityName);
            if (entityType == null)
                throw new SqlParserException(new SqlError(null, 0, 0, $"Not found entity with name '{entityName}'"));
            return AllEntities.Inst[entityType].TableDescriptor;
        }

        private object bindRecord(SqlDbQuery query, SqlSelectStatement select)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            int fieldCount = query.FieldCount;
            ;
            for (int i = 0; i < fieldCount; i++)
            {
                string name = query.Field(i).Name;
                object value = query.GetValue(i);
                Type toType = null;

                SqlStatement.AliasEntry aliasEntry = select.AliasEntrys.Find(name);
                if(aliasEntry != null)
                {
                    toType = select.AliasEntrys.Find(name).Expression.RealType;
                }
                else
                {
                    foreach(SqlStatement.EntityEntry entityEntry in select.EntityEntrys)
                    {
                        EntityDescriptor entityDescriptor = entityEntry.EntityDescriptor;
                        name = mBuilder.NameByField(entityDescriptor.EntityType, name);
                        if (name != null)
                        {
                            toType = mBuilder.TypeByName(entityDescriptor.EntityType, name);
                            break;
                        }
                    }
                    if (name == null)
                    {
                        name = query.Field(i).Name;
                    }
                }

                if (value != null)
                {
                    if (value.GetType().FullName == "System.DBNull")
                    {
                        value = null;
                    }
                    else
                    {
                        if (toType != null)
                        {
                            value = query.LanguageSpecifics.TranslateValue(value, toType);
                        }
                    }
                }

                try
                {
                    result.Add(name, value);
                }
                catch
                {

                }
            }
            return result;
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

        private string getParameter(string parameterName)
        {
            if (parameterName == null)
                return null;

            string prefix = mConnection.GetLanguageSpecifics().ParameterInQueryPrefix;
            if (!string.IsNullOrEmpty(prefix) && !parameterName.StartsWith(prefix))
                parameterName = prefix + parameterName;
            return parameterName;
        }

        private void bindParams(SqlDbQuery query)
        {
            foreach (KeyValuePair<string, object> pair in mBindParams)
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

    internal class MySelectQueryBuilder : SelectQueryBuilder
    {
        public MySelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor mainTable) : base(specifics, mainTable)
        {
        }
        internal QueryBuilderEntity AddTable(TableDescriptor table, TableJoinType joinType) => base.AddTable(table, null, joinType, null, null);
    }

}
