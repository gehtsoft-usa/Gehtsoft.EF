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

[assembly: InternalsVisibleTo("Gehtsoft.EF.Db.SqlDb.Sql.Test")]

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

        internal bool WhetherParseToLinq {get; set;} = false;
        public StatementSetEnvironment Parse(string name, TextReader source)
        {
            var root = ParseToRawTree(name, source);
            TopEnvironment = null;
            mLastParse = ParseNode(name, root); // for possible run later
            return mLastParse;
        }

        internal StatementSetEnvironment ParseNode(string name, ASTNode root, Statement parentStatement = null)
        {
            bool saveWhetherParseToLinq = WhetherParseToLinq;
            WhetherParseToLinq = false;
            var visitor = new SqlASTVisitor();
            StatementSetEnvironment initialSet = new StatementSetEnvironment();
            initialSet.ParentStatement = parentStatement;
            StatementSetEnvironment result = visitor.VisitStatements(this, name, root, initialSet);
            WhetherParseToLinq = saveWhetherParseToLinq;
            return result;
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

        public Expression ParseToLinq(string name, TextReader source)
        {
            var root = ParseToRawTree(name, source);
            return ParseNodeToLinq(name, root, null, true);
        }

        internal Expression ParseNodeToLinq(string name, ASTNode root, Statement parentStatement, bool clear = false)
        {
            bool saveWhetherParseToLinq = WhetherParseToLinq;
            WhetherParseToLinq = true;
            var visitor = new SqlASTVisitor();
            Expression result = visitor.VisitStatementsToLinq(this, name, root, parentStatement?.Type ?? Statement.StatementType.Block, parentStatement?.OnContinue, clear);
            WhetherParseToLinq = saveWhetherParseToLinq;
            return result;
        }

        public Expression ParseToLinq(string name, string source)
        {
            using (var reader = new StringReader(source))
            {
                return ParseToLinq(name, reader);
            }
        }
        public Expression ParseToLinq(string fileName, Encoding encoding = null)
        {
            using (StreamReader sr = new StreamReader(fileName, encoding ?? Encoding.UTF8, true))
            {
                return ParseToLinq(fileName, sr);
            }
        }

        protected Dictionary<string, Type> mTypeNameToEntity = new Dictionary<string, Type>();
        protected Dictionary<Type, List<Tuple<string, string, Type>>> mTypeToFields = new Dictionary<Type, List<Tuple<string, string, Type>>>();

        internal Type TypeByName(Type entityType, string name) => mTypeToFields[entityType].Where(t => t.Item1 == name).SingleOrDefault()?.Item3;
        internal string FieldByName(Type entityType, string name) => mTypeToFields[entityType].Where(t => t.Item1 == name).SingleOrDefault()?.Item2;
        internal string NameByField(Type entityType, string fieldName) => mTypeToFields[entityType].Where(t => t.Item2 == fieldName).SingleOrDefault()?.Item1;
        internal Type EntityByName(string name) => mTypeNameToEntity.ContainsKey(name) ? mTypeNameToEntity[name] : null;

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

        public SqlDbConnection Connection { get; private set; } = null;
        public SqlCodeDomBuilder NewEnvironment(SqlDbConnection connection)
        {
            SqlCodeDomBuilder retval = NewEnvironment();
            retval.Connection = connection;

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
            TopEnvironment = null;
            return Run(connection, mLastParse, false);
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
                if (current == null && BlockDescriptors.Count > 0)
                {
                    return BlockDescriptors.Peek().LastStatementResult;
                }
                return new List<object>();
            }
        }

        public object Run(SqlDbConnection connection, StatementSetEnvironment statements, bool inner = false)
        {
            statements.ClearEnvironment();
            statements.ParentEnvironment = TopEnvironment;
            TopEnvironment = statements;
            statements.LastStatementResult = null;
            bool cont = true;
            while (cont)
            {
                statements.Leave = false;
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
                            case Statement.StatementType.If:
                                IfRunner ifRunner = new IfRunner(this, connection);
                                object ifResult = ifRunner.Run(statement as IfStatement);
                                if (ifResult != null)
                                {
                                    statements.LastStatementResult = ifResult;
                                }
                                break;
                            case Statement.StatementType.Continue:
                                ContinueRunner continueRunner = new ContinueRunner(this, connection, statements);
                                continueRunner.Run(statement as ContinueStatement);
                                break;
                            case Statement.StatementType.Break:
                                BreakRunner breakRunner = new BreakRunner(this, connection, statements);
                                breakRunner.Run(statement as BreakStatement);
                                break;
                            case Statement.StatementType.Loop:
                            case Statement.StatementType.Block:
                                BlockRunner blockRunner = new BlockRunner(this, connection);
                                object blockDoResult = blockRunner.Run(statement as BlockStatement);
                                if (blockDoResult != null)
                                {
                                    statements.LastStatementResult = blockDoResult;
                                }
                                break;
                            case Statement.StatementType.Switch:
                                SwitchRunner switchRunner = new SwitchRunner(this, connection);
                                object switchResult = switchRunner.Run(statement as SwitchStatement);
                                if (switchResult != null)
                                {
                                    statements.LastStatementResult = switchResult;
                                }
                                break;
                            case Statement.StatementType.AddField:
                                AddFieldStatement addFieldStatement = statement as AddFieldStatement;
                                addFieldStatement.Run(connection);
                                break;
                            case Statement.StatementType.AddRow:
                                AddRowStatement addRowStatement = statement as AddRowStatement;
                                addRowStatement.Run(connection);
                                break;
                            case Statement.StatementType.DeclareCursor:
                                DeclareCursorStatement declareCursorStatement = statement as DeclareCursorStatement;
                                declareCursorStatement.Run();
                                break;
                            case Statement.StatementType.OpenCursor:
                                OpenCursorStatement openCursorStatement = statement as OpenCursorStatement;
                                openCursorStatement.Run(connection);
                                break;
                            case Statement.StatementType.CloseCursor:
                                CloseCursorStatement closeCursorStatement = statement as CloseCursorStatement;
                                closeCursorStatement.Run();
                                break;
                            case Statement.StatementType.Assign:
                                AssignStatement assignStatement = statement as AssignStatement;
                                assignStatement.Run();
                                break;
                        }
                    }
                    if (statements.Leave)
                    {
                        break;
                    }
                }

                if (statements.Continue)
                {
                    if (statements.BeforeContinue != null)
                    {
                        Run(connection, statements.BeforeContinue, true);
                    }
                    cont = true;
                    statements.Continue = false;
                }
                else
                {
                    cont = false;
                }
            }
            TopEnvironment = statements.ParentEnvironment;
            object result = statements.LastStatementResult;
            if (!inner)
            {
                this.ClearOpenedQueries();
            }
            return result;
        }

        internal IStatementSetEnvironment TopEnvironment { get; set; } = null;

        internal Stack<BlockDescriptor> BlockDescriptors { get; set; } = new Stack<BlockDescriptor>();

        internal static void PushDescriptor(SqlCodeDomBuilder codeDomBuilder, LabelTarget startLabel, LabelTarget endLabel, Statement.StatementType statementType)
        {
            BlockDescriptor descr = new BlockDescriptor();
            descr.StartLabel = startLabel;
            descr.EndLabel = endLabel;
            descr.StatementType = statementType;
            codeDomBuilder.BlockDescriptors.Push(descr);
        }
        internal static object PopDescriptor(SqlCodeDomBuilder codeDomBuilder)
        {
            object retval = codeDomBuilder.BlockDescriptors.Peek().LastStatementResult;
            codeDomBuilder.BlockDescriptors.Pop();
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

        private IParametersHolder findEnvironmentWithParameter(string name, bool local = false)
        {
            IStatementSetEnvironment current = TopEnvironment;
            while (current != null)
            {
                if (current.ContainsGlobalParameter(name))
                    return current;
                current = current.ParentEnvironment;
                if (local) break;
            }
            if (current == null && BlockDescriptors.Count > 0)
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

        internal bool AddGlobalParameter(string name, ResultTypes resultType, bool local = false)
        {
            IParametersHolder found = findEnvironmentWithParameter(name, local);
            if (found != null)
                return false;
            if (TopEnvironment != null)
                TopEnvironment.AddGlobalParameter(name, new SqlConstant(null, resultType));
            if (BlockDescriptors.Count > 0)
                BlockDescriptors.Peek().AddGlobalParameter(name, new SqlConstant(null, resultType));
            return true;
        }

        internal void UpdateGlobalParameter(string name, SqlConstant value)
        {
            IParametersHolder found = findEnvironmentWithParameter(name);
            if (found == null)
            {
                if (TopEnvironment != null)
                    TopEnvironment.AddGlobalParameter(name, value);
                if (BlockDescriptors.Count > 0)
                    BlockDescriptors.Peek().AddGlobalParameter(name, value);
            }
            else
                found.UpdateGlobalParameter(name, value);
        }

        internal SqlConstant FindGlobalParameter(string name)
        {
            IParametersHolder found = findEnvironmentWithParameter(name);
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
                    throw new SqlParserException(new SqlError(null, 0, 0, $"Runtime error while SET execution"));
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
            foreach(KeyValuePair<Guid, SqlDbQuery> item in mOpenedQueries)
            {
                item.Value.Dispose();
            }
            mOpenedQueries = new Dictionary<Guid, SqlDbQuery>();
        }
    }

    internal class BlockDescriptor : IParametersHolder
    {
        internal Expression OnContinue { get; set; } = null;
        internal LabelTarget StartLabel { get; set; }
        internal LabelTarget EndLabel { get; set; }
        internal Statement.StatementType StatementType { get; set; }

        private Dictionary<string, SqlConstant> globalParameters = new Dictionary<string, SqlConstant>();

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
            if (!globalParameters.ContainsKey(name))
                globalParameters.Add(name, value);
            else
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

        private object mLastStatementResult = new List<object>();

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

        internal object LastStatementResult
        {
            get
            {
                return mLastStatementResult;
            }
            set
            {
                mLastStatementResult = value;
            }
        }
    }
}