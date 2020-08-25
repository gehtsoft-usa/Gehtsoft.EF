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
        private SelectQueryBuilder mMainBuilder = null;
        private EntityDescriptor mMainEntityDescriptor = null;

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

                using (SqlDbQuery query = mConnection.GetQuery(mMainBuilder))
                {
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

        private void processFrom(SqlFromClause fromClause)
        {
            int firstPrimary = 0;
            foreach (SqlTableSpecification table in fromClause.TableCollection)
            {
                if (table.Type == SqlTableSpecification.TableType.Primary)
                {
                    break;
                }
                firstPrimary++;
            }
            if (firstPrimary >= fromClause.TableCollection.Count)
                throw new SqlParserException(new SqlError(null, 0, 0, $"No primary entity in FROM clause"));

            mMainBuilder = createBuilder(((SqlPrimaryTable)fromClause.TableCollection[firstPrimary]).TableName, out mMainEntityDescriptor);

            int i = 0;
            foreach (SqlTableSpecification table in fromClause.TableCollection)
            {
                if (firstPrimary != i)
                {
                    if (table.Type == SqlTableSpecification.TableType.Primary)
                    {
                        mMainBuilder.AddTable(findTableDescriptor(((SqlPrimaryTable)table).TableName), false);
                    }
                }
                i++;
            }
        }

        private SelectQueryBuilder createBuilder(string entityName, out EntityDescriptor entityDescriptor)
        {
            Type entityType = mBuilder.EntityByName(entityName);
            if (entityType == null)
                throw new SqlParserException(new SqlError(null, 0, 0, $"Not found entity with name '{entityName}'"));
            entityDescriptor = AllEntities.Inst[entityType];
            return new SelectQueryBuilder(mConnection.GetLanguageSpecifics(), entityDescriptor.TableDescriptor);
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

    }
}
