using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class SqlPrimaryTable : SqlTableSpecification
    {
        public string TableName { get; }
        public string CorrelationName { get; }
        public override TableType Type
        {
            get
            {
                return TableType.Primary;
            }
        }

        internal SqlPrimaryTable(SqlStatement parentStatement, ASTNode fieldNode, string source)
        {
            if (fieldNode.Children.Count > 1)
            {
                TableName = fieldNode.Children[0].Value;
                CorrelationName = fieldNode.Children[1].Value;
            }
            else
            {
                TableName = fieldNode.Children[0].Value;
            }

            try
            {
                parentStatement.AddEntityEntry(TableName, CorrelationName);
            }
            catch
            {
                throw new SqlParserException(new SqlError(source,
                    fieldNode.Position.Line,
                    fieldNode.Position.Column,
                    $"Not found entity with name '{TableName}'"));
            }
        }
        internal SqlPrimaryTable(SqlStatement parentStatement, string tableName, string correlationName)
        {
            TableName = tableName;
            CorrelationName = correlationName;
            try
            {
                parentStatement.AddEntityEntry(TableName, CorrelationName);
            }
            catch
            {
                throw new SqlParserException(new SqlError(null, 0, 0, $"Not found entity with name '{TableName}'"));
            }
        }
        internal SqlPrimaryTable(SqlStatement parentStatement, string tableName) :
            this(parentStatement, tableName, null)
        {
            TableName = tableName;
            CorrelationName = null;
        }

        public virtual bool Equals(SqlPrimaryTable other)
        {
            if (other == null)
                return false;
            return (this.TableName == other.TableName && this.CorrelationName == other.CorrelationName);
        }

        public override bool Equals(SqlTableSpecification obj)
        {
            if (obj is SqlPrimaryTable item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}
