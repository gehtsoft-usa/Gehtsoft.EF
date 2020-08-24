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

[assembly: InternalsVisibleTo("Gehtsoft.EF.Db.SqlDb.Sql.Test,PublicKey="+
"00240000048000009400000006020000002400005253413100040000010001005d19d6f6a54328"+
"9d63039adebf287aeb946fb5920d9318135d576d3b8eef0e9e8f81bfc95e6735e7bfbe059ed389"+
"cacf829780c9b2a5095dd47c15f10d40f1843828c85a6232802d1a21dafe16f1381facd2b11008"+
"e6be0ab0795400f6c5d12c76f2ea5dcd82464fb5f4a0589097346872f683e3bca6d4ec9ed917dc"+
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

        private SqlStatementCollection mLastParse = null;

        public SqlStatementCollection Parse(string name, TextReader source)
        {
            var root = ParseToRawTree(name, source);
            var visitor = new SqlASTVisitor();
            mLastParse = visitor.VisitStatements(this, name, root); // for possible run later
            return mLastParse;
        }
        public SqlStatementCollection Parse(string name, string source)
        {
            using (var reader = new StringReader(source))
            {
                return Parse(name, reader);
            }
        }
        public SqlStatementCollection Parse(string fileName, Encoding encoding = null)
        {
            using (StreamReader sr = new StreamReader(fileName, encoding ?? Encoding.UTF8, true))
            {
                return Parse(fileName, sr);
            }
        }

        private Dictionary<string, Type> mTypeNameToEntity = new Dictionary<string, Type>();
        private Dictionary<Type, List<Tuple<string, string, Type>>> mTypeToFields = new Dictionary<Type, List<Tuple<string, string, Type>>>();

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

                        mTypeToFields[entity.EntityType].Add(Tuple.Create(propertyAccessor.Name, fieldName, propertyAttribute.ForeignKey ? typeof(int) : propertyInfo.PropertyType));
                    }
                }
            }
        }

        public object Run(ISqlDbConnectionFactory connectionFactory)
        {
            object result = null;
            if (mLastParse == null)
                throw new ArgumentException("Nothing parsed yet");

            foreach(SqlStatement statement in mLastParse)
            {
                switch(statement.Id)
                {
                    case SqlStatement.StatementId.Select:
                        SelectRunner runner = new SelectRunner(this, connectionFactory);
                        result = runner.Run(statement as SqlSelectStatement);
                        break;

                    default:
                        throw new Exception($"Unknown statement '{statement.Id}'");
                }
            }

            return result;
        }
    }
}