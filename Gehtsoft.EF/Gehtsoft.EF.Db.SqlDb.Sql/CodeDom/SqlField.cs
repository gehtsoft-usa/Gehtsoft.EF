using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class SqlField : SqlBaseExpression
    {
        public string Name { get; }
        public string Prefix { get; }
        private ResultTypes mResultType = ResultTypes.Unknown;

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


        internal SqlField(SqlStatement parentStatement, ASTNode fieldNode, string source)
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
        internal SqlField(SqlStatement parentStatement, string name, string prefix)
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
        internal SqlField(SqlStatement parentStatement, string name)
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

        private void processField(SqlStatement parentStatement, string prefix, string name, out string error)
        {
            error = null;
            Type entityType = parentStatement.EntityEntrys[0].EntityType;
            if (prefix != null)
            {
                if (!parentStatement.EntityEntrys.Exists(prefix))
                {
                    error = $"Not found entity '{prefix}'";
                    return;
                }
                entityType = parentStatement.EntityEntrys.Find(prefix).EntityType;
            }
            Type fieldType = parentStatement.CodeDomBuilder.TypeByName(entityType, name);
            if (fieldType == null && prefix == null && !parentStatement.IgnoreAlias)
            {
                if(parentStatement.AliasEntrys.Exists(name))
                {
                    fieldType = parentStatement.AliasEntrys.Find(name).Expression.RealType;
                }
            }
            if (fieldType == null)
            {
                error = $"Not found field '{name}' in entity '{entityType.Name}'";
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
