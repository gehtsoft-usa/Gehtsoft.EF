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
using System.Linq.Expressions;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.Statement;

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

        internal Expression ParseNodeToLinq(string name, ASTNode root, Statement parentStatement, bool clear = false)
        {
            var visitor = new SqlAstVisitor();
            Expression result = visitor.VisitStatementsToLinq(this, name, root, parentStatement?.Type ?? Statement.StatementType.Block, parentStatement?.OnContinue, clear);
            return result;
        }

        internal Expression Parse(string name, TextReader source)
        {
            var root = ParseToRawTree(name, source);
            try
            {
                return ParseNodeToLinq(name, root, null, true);
            }
            catch
            {
                while (this.BlockDescriptors.Count > 0) this.BlockDescriptors.Pop();
                throw;
            }
        }

        internal Expression Parse(string name, string source)
        {
            using (var reader = new StringReader(source))
            {
                return Parse(name, reader);
            }
        }
        internal Expression Parse(string fileName, Encoding encoding = null)
        {
            using (StreamReader sr = new StreamReader(fileName, encoding ?? Encoding.UTF8, true))
            {
                return Parse(fileName, sr);
            }
        }

        protected Dictionary<string, Type> mTypeNameToEntity = new Dictionary<string, Type>();
        protected Dictionary<Type, List<Tuple<string, string, Type>>> mTypeToFields = new Dictionary<Type, List<Tuple<string, string, Type>>>();

        internal Type TypeByName(Type entityType, string name) => mTypeToFields[entityType].SingleOrDefault(t => t.Item1 == name)?.Item3;
        internal string FieldByName(Type entityType, string name) => mTypeToFields[entityType].SingleOrDefault(t => t.Item1 == name)?.Item2;
        internal string NameByField(Type entityType, string fieldName) => mTypeToFields[entityType].SingleOrDefault(t => t.Item2.Equals(fieldName, StringComparison.InvariantCultureIgnoreCase))?.Item1;
        internal Type EntityByName(string name) => mTypeNameToEntity.ContainsKey(name) ? mTypeNameToEntity[name] : null;

        public void Build(EntityFinder.EntityTypeInfo[] entities, string ns = "NS")
        {
            foreach (var entity in entities)
            {
                string name = entity.EntityType.Name;
                mTypeNameToEntity[name] = entity.EntityType;

                mTypeToFields.Add(entity.EntityType, new List<Tuple<string, string, Type>>());

                foreach (PropertyInfo propertyInfo in entity.EntityType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    PropertyAccessor propertyAccessor = new PropertyAccessor(propertyInfo);
                    EntityPropertyAttribute propertyAttribute = propertyAccessor.GetCustomAttribute<EntityPropertyAttribute>();
                    if (propertyAttribute != null)
                    {
                        string fieldName = propertyAttribute.Field ?? propertyAccessor.Name.ToLower();

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

        public SqlDbConnection Connection { get; private set; } = null;

        internal IDictionary<string, object> ParametersDictionary;

        public SqlCodeDomEnvironment NewEnvironment()
        {
            SqlCodeDomEnvironment retval = new SqlCodeDomEnvironment();
            retval.SqlCodeDomBuilder.mTypeNameToEntity = mTypeNameToEntity;
            retval.SqlCodeDomBuilder.mTypeToFields = mTypeToFields;

            return retval;
        }

        public SqlCodeDomEnvironment NewEnvironment(SqlDbConnection connection)
        {
            SqlCodeDomEnvironment retval = NewEnvironment();
            retval.SqlCodeDomBuilder.Connection = connection;

            return retval;
        }

        internal object LastStatementResult
        {
            get
            {
                if (BlockDescriptors.Count > 0)
                {
                    return BlockDescriptors.Peek().LastStatementResult;
                }
                return new List<object>();
            }
        }

        internal Stack<BlockDescriptor> BlockDescriptors { get; set; } = new Stack<BlockDescriptor>();

        internal static void PushDescriptor(SqlCodeDomBuilder codeDomBuilder, LabelTarget startLabel, LabelTarget endLabel, Statement.StatementType statementType)
        {
            BlockDescriptor descr = new BlockDescriptor
            {
                StartLabel = startLabel,
                EndLabel = endLabel,
                StatementType = statementType
            };
            codeDomBuilder.BlockDescriptors.Push(descr);
        }
        internal static object PopDescriptor(SqlCodeDomBuilder codeDomBuilder)
        {
            object retval = codeDomBuilder.BlockDescriptors.Peek().LastStatementResult;
            BlockDescriptor descr = codeDomBuilder.BlockDescriptors.Pop();
            if (descr.StatementType == StatementType.DummyPersist)
            {
                if (codeDomBuilder.BlockDescriptors.Count > 0)
                {
                    foreach (var item in descr.All)
                    {
                        codeDomBuilder.AddGlobalParameter(item.Key, item.Value.ResultType);
                    }
                }
            }
            return retval;
        }
        internal Expression StartBlock(LabelTarget startLabel, LabelTarget endLabel, Statement.StatementType statementType)
        {
            return Expression.Call(typeof(SqlCodeDomBuilder), "PushDescriptor", null,
                Expression.Constant(this),
                Expression.Constant(startLabel),
                Expression.Constant(endLabel),
                Expression.Constant(statementType)
                );
        }

        internal Expression EndBlock()
        {
            return Expression.Call(typeof(SqlCodeDomBuilder), "PopDescriptor", null, Expression.Constant(this));
        }

        private IParametersHolder FindEnvironmentWithParameter(string name)
        {
            if (BlockDescriptors.Count > 0)
            {
                BlockDescriptor[] array = BlockDescriptors.ToArray();
                for (int i = array.Length - 1; i >= 0; i--)
                {
                    BlockDescriptor descr = array[i];
                    if (descr.ContainsGlobalParameter(name))
                        return descr;
                }
            }
            return null;
        }

        internal bool AddGlobalParameter(string name, ResultTypes resultType)
        {
            IParametersHolder found = FindEnvironmentWithParameter(name);
            if (found != null)
                return false;
            if (BlockDescriptors.Count > 0)
                BlockDescriptors.Peek().AddGlobalParameter(name, new SqlConstant(null, resultType));
            return true;
        }

        internal void UpdateGlobalParameter(string name, SqlConstant value)
        {
            IParametersHolder found = FindEnvironmentWithParameter(name);
            if (found == null)
            {
                if (BlockDescriptors.Count > 0)
                    BlockDescriptors.Peek().AddGlobalParameter(name, value);
            }
            else
                found.UpdateGlobalParameter(name, value);
        }

        internal SqlConstant FindGlobalParameter(string name)
        {
            IParametersHolder found = FindEnvironmentWithParameter(name);
            if (found != null)
                return found.FindGlobalParameter(name);
            return null;
        }

        internal void ExitRun(SqlBaseExpression exitExpression)
        {
            object exitValue = null;
            if (exitExpression != null)
            {
                SqlConstant resultConstant = StatementRunner.CalculateExpression(exitExpression, this, Connection);
                if (resultConstant == null)
                {
                    throw new SqlParserException(new SqlError(null, 0, 0, "Runtime error while SET execution"));
                }
                exitValue = resultConstant.Value;
            }
            while (BlockDescriptors.Count > 1)
            {
                BlockDescriptor current = BlockDescriptors.Peek();
                if (exitValue != null)
                {
                    current.LastStatementResult = exitValue;
                }
                else if (current.LastStatementResult != null)
                {
                    exitValue = current.LastStatementResult;
                }
                BlockDescriptors.Pop();
            }

            if (exitValue != null)
            {
                BlockDescriptors.Peek().LastStatementResult = exitValue;
            }
        }

        internal void BreakRun()
        {
            while (BlockDescriptors.Count > 0)
            {
                BlockDescriptor current = BlockDescriptors.Peek();
                if (current.StatementType == StatementType.Loop || current.StatementType == StatementType.Switch)
                {
                    break;
                }
                BlockDescriptors.Pop();
            }
        }

        internal void ContinueRun()
        {
            while (BlockDescriptors.Count > 0)
            {
                BlockDescriptor current = BlockDescriptors.Peek();
                if (current.StatementType == StatementType.Loop)
                {
                    break;
                }
                BlockDescriptors.Pop();
            }
        }

        private Dictionary<Guid, SqlDbQuery> mOpenedQueries = new Dictionary<Guid, SqlDbQuery>();

        internal void AddOpenedQuery(Guid guid, SqlDbQuery query)
        {
            mOpenedQueries.Add(guid, query);
        }

        internal void RemoveOpenedQuery(Guid guid)
        {
            mOpenedQueries.Remove(guid);
        }

        internal void ClearOpenedQueries()
        {
            foreach (KeyValuePair<Guid, SqlDbQuery> item in mOpenedQueries)
            {
                item.Value.Dispose();
            }
            mOpenedQueries = new Dictionary<Guid, SqlDbQuery>();
        }
    }

    public class SqlCodeDomEnvironment
    {
        internal SqlCodeDomBuilder SqlCodeDomBuilder { get; }
        public SqlCodeDomEnvironment()
        {
            SqlCodeDomBuilder = new SqlCodeDomBuilder();
        }
        public Func<IDictionary<string, object>, dynamic> Parse(string name, TextReader source)
        {
            Func<object> compiled = Expression.Lambda<Func<dynamic>>(SqlCodeDomBuilder.Parse(name, source)).Compile();
            Func<IDictionary<string, object>, dynamic> func = (arg) =>
            {
                 SqlCodeDomBuilder.ParametersDictionary = arg;
                 return compiled();
            };
            return func;
        }

        public Func<IDictionary<string, object>, dynamic> Parse(string name, string source)
        {
            using (TextReader tr = new StringReader(source))
                return Parse(name, tr);
        }

        public Func<IDictionary<string, object>, dynamic> Parse(string fileName, Encoding encoding = null)
        {
            using (TextReader tr = new StreamReader(fileName, encoding))
                return Parse(fileName, tr);
        }
    }

    internal class BlockDescriptor : IParametersHolder
    {
        internal Expression OnContinue { get; set; } = null;
        internal LabelTarget StartLabel { get; set; }
        internal LabelTarget EndLabel { get; set; }
        internal Statement.StatementType StatementType { get; set; }

        private readonly Dictionary<string, SqlConstant> globalParameters = new Dictionary<string, SqlConstant>();

        internal IDictionary<string, SqlConstant> All => globalParameters;

        bool IParametersHolder.AddGlobalParameter(string name, SqlConstant value) => AddGlobalParameter(name, value);
        internal bool AddGlobalParameter(string name, SqlConstant value)
        {
            if (globalParameters.ContainsKey(name))
                return false;
            globalParameters.Add(name, value);
            return true;
        }

        void IParametersHolder.UpdateGlobalParameter(string name, SqlConstant value) => UpdateGlobalParameter(name, value);
        internal void UpdateGlobalParameter(string name, SqlConstant value)
        {
            globalParameters[name] = value;
        }

        SqlConstant IParametersHolder.FindGlobalParameter(string name) => FindGlobalParameter(name);
        internal SqlConstant FindGlobalParameter(string name)
        {
            if (globalParameters.ContainsKey(name))
                return globalParameters[name];
            return null;
        }
        bool IParametersHolder.ContainsGlobalParameter(string name) => ContainsGlobalParameter(name);
        internal bool ContainsGlobalParameter(string name)
        {
            return globalParameters.ContainsKey(name);
        }

        object IParametersHolder.LastStatementResult
        {
            get
            {
                return LastStatementResult;
            }
            set
            {
                LastStatementResult = value;
            }
        }

        internal object LastStatementResult { get; set; } = new List<object>();
    }
}