using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using System;
using System.Collections.Generic;
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

                using (SqlDbQuery query = mConnection.GetQuery(mMainBuilder))
                {
                    query.ExecuteReader();
                    while (query.ReadNext())
                    {
                        object o = Bind(query);
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


        public object Bind(SqlDbQuery query)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            int fieldCount = query.FieldCount;
            EntityDescriptor entityDescriptor;
            for (int i = 0; i < fieldCount; i++)
            {
                string name = query.Field(i).Name;
                Dictionary<string, object> placeTo = result;

                entityDescriptor = mMainEntityDescriptor;

                object value = query.GetValue(i);
                string nameOrg = name;
                name = mBuilder.NameByField(entityDescriptor.EntityType, name);

                if (name != null)
                {
                    if (value != null)
                    {
                        if (value.GetType().FullName == "System.DBNull")
                        {
                            if (mBuilder.TypeByName(entityDescriptor.EntityType, name) == null)
                            {
                                //continue; // for foreign keys
                                name = nameOrg;
                            }
                            value = null;
                        }
                        else
                        {
                            Type toType = mBuilder.TypeByName(entityDescriptor.EntityType, name);
                            if (toType != null)
                            {
                                value = query.LanguageSpecifics.TranslateValue(value, toType);
                            }
                            else
                            {
                                //continue; // for foreign keys
                                name = nameOrg;
                            }
                        }
                    }
                }
                else
                {
                    name = query.Field(i).Name;
                }

                try
                {
                    placeTo.Add(name, value);
                }
                catch
                {

                }
            }
            return result;
        }

    }
}
