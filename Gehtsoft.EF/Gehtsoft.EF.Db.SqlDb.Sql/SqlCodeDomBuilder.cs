using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using Gehtsoft.EF.Entities;
using Hime.Redist;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

[assembly: InternalsVisibleTo("Gehtsoft.EF.Db.SqlDb.Sql.Test,PublicKey=" +
"00240000048000009400000006020000002400005253413100040000010001005d19d6f6a54328" +
"9d63039adebf287aeb946fb5920d9318135d576d3b8eef0e9e8f81bfc95e6735e7bfbe059ed389" +
"cacf829780c9b2a5095dd47c15f10d40f1843828c85a6232802d1a21dafe16f1381facd2b11008" +
"e6be0ab0795400f6c5d12c76f2ea5dcd82464fb5f4a0589097346872f683e3bca6d4ec9ed917dc" +
"9276c1cf")]

namespace Gehtsoft.EF.Db.SqlDb.Sql
{
    /// <summary>
    /// Code DOM builder for a Sql source.
    /// </summary>
    public class SqlCodeDomBuilder
    {
        /// <summary>
        /// Builds the source into a raw AST tree (a service/debug method, use BuildDom methods instead)
        /// </summary>
        /// <param name="name">The name</param>
        /// <param name="source"></param>
        /// <returns></returns>
        internal ASTNode ParseToRawTree(string name, TextReader source)
        {
            SqlParser parser = new SqlParser(new SqlLexer(source));
            ParseResult result = parser.Parse();
            if (!result.IsSuccess)
            {
                var errors = SqlErrorCollection.ToSqlErrors(name, result);
                throw new SqlParserException(errors);
            }
            return result.Root;
        }

        private StatementSetEnvironment mLastParse = null;

        public StatementSetEnvironment Parse(string name, TextReader source)
        {
            var root = ParseToRawTree(name, source);
            var visitor = new SqlASTVisitor();
            StatementSetEnvironment initialSet = new StatementSetEnvironment();
            initialSet.ParentEnvironment = null;
            initialSet.ParentStatement = null;
            mLastParse = visitor.VisitStatements(this, name, root, initialSet); // for possible run later
            return mLastParse;
        }
        public StatementSetEnvironment Parse(string name, string source)
        {
            using (var reader = new StringReader(source))
            {
                return Parse(name, reader);
            }
        }
        public StatementSetEnvironment Parse(string fileName, Encoding encoding = null)
        {
            using (StreamReader sr = new StreamReader(fileName, encoding ?? Encoding.UTF8, true))
            {
                return Parse(fileName, sr);
            }
        }

        protected Dictionary<string, Type> mTypeNameToEntity = new Dictionary<string, Type>();
        protected Dictionary<Type, List<Tuple<string, string, Type>>> mTypeToFields = new Dictionary<Type, List<Tuple<string, string, Type>>>();

        public Type TypeByName(Type entityType, string name) => mTypeToFields[entityType].Where(t => t.Item1 == name).SingleOrDefault()?.Item3;
        public string FieldByName(Type entityType, string name) => mTypeToFields[entityType].Where(t => t.Item1 == name).SingleOrDefault()?.Item2;
        public string NameByField(Type entityType, string fieldName) => mTypeToFields[entityType].Where(t => t.Item2 == fieldName).SingleOrDefault()?.Item1;
        public Type EntityByName(string name) => mTypeNameToEntity.ContainsKey(name) ? mTypeNameToEntity[name] : null;

        public void Build(EntityFinder.EntityTypeInfo[] entities, string ns = "NS")
        {
            foreach (var entity in entities)
            {
                string name = entity.EntityType.Name;
                mTypeNameToEntity[name] = entity.EntityType;
                EntityDescriptor descriptor = AllEntities.Inst[entity.EntityType];

                mTypeToFields.Add(entity.EntityType, new List<Tuple<string, string, Type>>());

                foreach (PropertyInfo propertyInfo in entity.EntityType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    PropertyAccessor propertyAccessor = new PropertyAccessor(propertyInfo);
                    EntityPropertyAttribute propertyAttribute = propertyAccessor.GetCustomAttribute<EntityPropertyAttribute>();
                    if (propertyAttribute != null)
                    {
                        string fieldName;
                        if (propertyAttribute.Field == null)
                            fieldName = propertyAccessor.Name.ToLower();
                        else
                            fieldName = propertyAttribute.Field;

                        Type propertyType = propertyInfo.PropertyType;
                        if (propertyAttribute.ForeignKey)
                        {
                            propertyType = null;
                            foreach (PropertyInfo innerPropertyInfo in propertyInfo.PropertyType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                            {
                                PropertyAccessor innerPropertyAccessor = new PropertyAccessor(innerPropertyInfo);
                                EntityPropertyAttribute innerPropertyAttribute = innerPropertyAccessor.GetCustomAttribute<EntityPropertyAttribute>();
                                if (innerPropertyAttribute != null)
                                {
                                    if (innerPropertyAttribute.PrimaryKey)
                                    {
                                        propertyType = innerPropertyInfo.PropertyType;
                                        break;
                                    }
                                }
                            }
                        }
                        mTypeToFields[entity.EntityType].Add(Tuple.Create(propertyAccessor.Name, fieldName, propertyType ?? typeof(int)));
                    }
                }
            }
        }

        public SqlCodeDomBuilder NewEnvironment()
        {
            SqlCodeDomBuilder retval = new SqlCodeDomBuilder();
            retval.mTypeNameToEntity = mTypeNameToEntity;
            retval.mTypeToFields = mTypeToFields;

            return retval;
        }

        public object Run(ISqlDbConnectionFactory connectionFactory)
        {
            object result = null;
            SqlDbConnection connection = connectionFactory.GetConnection();
            try
            {
                result = Run(connection);
            }
            finally
            {
                if (connectionFactory.NeedDispose)
                    connection.Dispose();
            }
            return result;
        }

        public object Run(SqlDbConnection connection)
        {
            if (mLastParse == null)
                throw new ArgumentException("Nothing parsed yet");

            mLastParse.ParentEnvironment = null;
            mLastParse.ParentStatement = null;
            TopEnvironment = mLastParse;
            return Run(connection, mLastParse);
        }

        internal object LastStatementResult
        {
            get
            {
                IStatementSetEnvironment current = TopEnvironment;
                while (current != null)
                {
                    if (current.LastStatementResult != null)
                        return current.LastStatementResult;
                    current = current.ParentEnvironment;
                }
                return new List<object>();
            }
        }

        internal object Run(SqlDbConnection connection, StatementSetEnvironment statements)
        {
            statements.ClearEnvironment();
            statements.LastStatementResult = null;
            bool cont = true;
            while (cont)
            {
                foreach (Statement statement in statements)
                {
                    if (statement is SqlStatement sqlStatement)
                    {
                        switch (sqlStatement.Id)
                        {
                            case SqlStatement.StatementId.Select:
                                SelectRunner selectRunner = new SelectRunner(this, connection);
                                statements.LastStatementResult = selectRunner.Run(sqlStatement as SqlSelectStatement);
                                break;

                            case SqlStatement.StatementId.Insert:
                                InsertRunner insertRunner = new InsertRunner(this, connection);
                                statements.LastStatementResult = insertRunner.Run(sqlStatement as SqlInsertStatement);
                                break;

                            case SqlStatement.StatementId.Update:
                                UpdateRunner updateRunner = new UpdateRunner(this, connection);
                                statements.LastStatementResult = updateRunner.Run(sqlStatement as SqlUpdateStatement);
                                break;

                            case SqlStatement.StatementId.Delete:
                                DeleteRunner deleteRunner = new DeleteRunner(this, connection);
                                statements.LastStatementResult = deleteRunner.Run(sqlStatement as SqlDeleteStatement);
                                break;

                            default:
                                throw new Exception($"Unknown statement '{sqlStatement.Id}'");
                        }
                    }
                    else
                    {
                        switch (statement.Type)
                        {
                            case Statement.StatementType.Set:
                                SetRunner setRunner = new SetRunner(this, connection);
                                setRunner.Run(statement as SetStatement);
                                break;
                            case Statement.StatementType.Exit:
                                ExitRunner exitRunner = new ExitRunner(this, connection, statements);
                                exitRunner.Run(statement as ExitStatement);
                                break;
                        }
                    }
                    if (statements.Leave)
                    {
                        statements.Leave = false;
                        break;
                    }
                }

                if (statements.Continue)
                {
                    cont = true;
                    statements.Continue = false;
                }
                else
                {
                    cont = false;
                }
            }
            TopEnvironment = statements.ParentEnvironment;
            return statements.LastStatementResult;
        }

        internal IStatementSetEnvironment TopEnvironment { get; set; } = null;

        private IStatementSetEnvironment findEnvironmentWithParameter(string name)
        {
            IStatementSetEnvironment current = TopEnvironment;
            while(current != null)
            {
                if (current.ContainsGlobalParameter(name))
                    return current;
                current = current.ParentEnvironment;
            }
            return null;
        }

        internal bool AddGlobalParameter(string name, ResultTypes resultType)
        {
            IStatementSetEnvironment found = findEnvironmentWithParameter(name);
            if (found != null)
                return false;
            TopEnvironment.AddGlobalParameter(name, new SqlConstant(null, resultType));
            return true;
        }

        internal void UpdateGlobalParameter(string name, SqlConstant value)
        {
            IStatementSetEnvironment found = findEnvironmentWithParameter(name);
            if (found == null)
                TopEnvironment.AddGlobalParameter(name, value);
            else
                found.UpdateGlobalParameter(name, value);
        }

        internal SqlConstant FindGlobalParameter(string name)
        {
            IStatementSetEnvironment found = findEnvironmentWithParameter(name);
            if (found != null)
                return found.FindGlobalParameter(name);
            return null;
        }
    }
}