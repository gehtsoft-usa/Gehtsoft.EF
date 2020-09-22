using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.Statement;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class SqlField : SqlBaseExpression
    {
        public EntityDescriptor EntityDescriptor { get; private set; } = null;
        public string Name { get; }
        public string Prefix { get; }
        private ResultTypes mResultType = ResultTypes.Unknown;
        private string mFieldName = null;
        public string FieldName
        {
            get
            {
                return mFieldName ?? Name;
            }
        }
        public override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.Field;
            }
        }
        public override ResultTypes ResultType
        {
            get
            {
                return mResultType;
            }
        }

        /// <summary>
        /// Result type can be determined in semantic analizer only
        /// </summary>
        /// <param name="resultType"></param>
        public virtual void SetResultType(ResultTypes resultType)
        {
            mResultType = resultType;
        }


        internal SqlField(Statement parentStatement, ASTNode fieldNode, string source)
        {
            if (fieldNode.Children.Count > 1)
            {
                Prefix = fieldNode.Children[0].Value;
                Name = fieldNode.Children[1].Value;
            }
            else
            {
                Name = fieldNode.Children[0].Value;
            }
            string error;
            processField(parentStatement, Prefix, Name, out error);
            if (error != null)
            {
                throw new SqlParserException(new SqlError(source,
                    fieldNode.Position.Line,
                    fieldNode.Position.Column,
                    error));
            }
        }
        internal SqlField(Statement parentStatement, string name, string prefix)
        {
            Name = name;
            Prefix = prefix;
            string error;
            processField(parentStatement, Prefix, Name, out error);
            if (error != null)
            {
                throw new SqlParserException(new SqlError(null, 0, 0, error));
            }
        }
        internal SqlField(Statement parentStatement, string name)
        {
            Name = name;
            Prefix = null;
            string error;
            processField(parentStatement, Prefix, Name, out error);
            if (error != null)
            {
                throw new SqlParserException(new SqlError(null, 0, 0, error));
            }
        }
        internal SqlField(string name, Type fieldType)
        {
            Name = name;
            Prefix = null;
            mResultType = GetResultType(fieldType);
        }

        private void processField(Statement parentStatement, string prefix, string name, out string error)
        {
            error = null;
            Type fieldType = null;
            if (prefix != null)
            {
                if (!parentStatement.EntityEntrys.Exists(prefix))
                {
                    error = $"Not found entity '{prefix}'";
                    return;
                }
                EntityEntry entityEntry = parentStatement.EntityEntrys.Find(prefix);
                fieldType = parentStatement.CodeDomBuilder.TypeByName(entityEntry.EntityType, name);
                EntityDescriptor = entityEntry.EntityDescriptor;
            }
            else
            {
                foreach (EntityEntry entityEntry in parentStatement.EntityEntrys)
                {
                    fieldType = parentStatement.CodeDomBuilder.TypeByName(entityEntry.EntityType, name);
                    if (fieldType != null)
                    {
                        EntityDescriptor = entityEntry.EntityDescriptor;
                        break;
                    }
                }
                if (fieldType == null && !parentStatement.IgnoreAlias)
                {
                    if (parentStatement.AliasEntrys.Exists(name))
                    {
                        AliasEntry aliasEntry = parentStatement.AliasEntrys.Find(name);
                        fieldType = aliasEntry.Expression.SystemType;
                        if(aliasEntry.Expression is SqlField aliasField)
                        {
                            EntityDescriptor = aliasField.EntityDescriptor;
                            mFieldName = aliasField.FieldName;
                        }
                    }
                }
            }
            if (fieldType == null)
            {
                error = $"Not found field '{name}' in entity";
                return;
            }

            mResultType = GetResultType(fieldType);
        }

        public virtual bool Equals(SqlField other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            return (this.Name == other.Name && this.Prefix == other.Prefix);
        }

        public override bool Equals(SqlBaseExpression obj)
        {
            if (obj is SqlField item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}
