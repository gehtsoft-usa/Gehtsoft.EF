using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    /// <summary>
    /// Insert statement
    /// </summary>
    public class SqlInsertStatement : SqlStatement
    {
        public SqlFieldCollection Fields { get; } = null;
        public SqlConstantCollection Values { get; } = null;
        public string TableName { get; } = null;
        private EntityDescriptor mEntityDescriptor = null;

        public EntityDescriptor EntityDescriptor
        {
            get
            {
                if (mEntityDescriptor == null)
                    mEntityDescriptor = this.EntityEntrys.Find(TableName).EntityDescriptor;
                return mEntityDescriptor;
            }
        }

        internal SqlInsertStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementId.Insert, currentSource, statementNode.Position.Line, statementNode.Position.Column)
        {
            TableName = statementNode.Children[0].Value;
            try
            {
                this.AddEntityEntry(TableName, null);
            }
            catch
            {
                throw new SqlParserException(new SqlError(currentSource,
                    statementNode.Children[0].Position.Line,
                    statementNode.Children[0].Position.Column,
                    $"Not found entity with name '{TableName}'"));
            }

            ASTNode fieldsNode = statementNode.Children[1];
            ASTNode valuesNode = statementNode.Children[2];

            Fields = new SqlFieldCollection();
            foreach (ASTNode fieldNode in fieldsNode.Children)
            {
                Fields.Add(new SqlField(this, fieldNode, currentSource));
            }

            Values = new SqlConstantCollection();
            foreach (ASTNode constantNode in valuesNode.Children)
            {
                Values.Add((SqlConstant)(SqlExpressionParser.ParseExpression(this, constantNode, currentSource)));
            }
            CheckFieldsAndValues();
        }

        internal SqlInsertStatement(SqlCodeDomBuilder builder, string tableName, SqlFieldCollection fields, SqlConstantCollection values)
            : base(builder, StatementId.Insert, null, 0, 0)
        {
            TableName = tableName;
            try
            {
                this.AddEntityEntry(TableName, null);
            }
            catch
            {
                throw new SqlParserException(new SqlError(null, 0, 0, $"Not found entity with name '{TableName}'"));
            }

            Fields = fields;
            Values = values;
        }

        internal protected void CheckFieldsAndValues()
        {
            if (Fields.Count != Values.Count)
            {
                throw new SqlParserException(new SqlError(null, 0, 0, $"Number of fields and values in INSERT statement should be the same"));
            }
            for (int i = 0; i < Fields.Count; i++)
            {
                SqlField field = Fields[i];
                SqlConstant constant = Values[i];

                if (constant.ResultType != SqlBaseExpression.ResultTypes.Unknown && constant.ResultType != field.ResultType)
                {
                    if (!(field.ResultType == SqlBaseExpression.ResultTypes.Double && constant.ResultType == SqlBaseExpression.ResultTypes.Integer))
                    {
                        throw new SqlParserException(new SqlError(null, 0, 0, $"Types of field and value in position {i + 1} of INSERT statement don't match"));
                    }
                }
                else if (constant.ResultType == SqlBaseExpression.ResultTypes.Unknown) //NULL
                {
                    if (!EntityDescriptor[field.Name].Nullable)
                    {
                        throw new SqlParserException(new SqlError(null, 0, 0, $"Field '{field.Name}' is not nullable"));
                    }
                }
            }

            foreach(TableDescriptor.ColumnInfo column in EntityDescriptor.TableDescriptor)
            {
                if(Fields.FindByName(column.ID) == null)
                {
                    if(!column.Nullable && column.DefaultValue == null && !(column.PrimaryKey && column.Autoincrement))
                    {
                        throw new SqlParserException(new SqlError(null, 0, 0, $"Value for the field '{column.ID}' should be set"));
                    }
                }
            }

        }

        public virtual bool Equals(SqlInsertStatement other)
        {
            if (other is SqlInsertStatement stmt)
            {
                return TableName.Equals(stmt.TableName) &&
                       Fields.Equals(stmt.Fields) &&
                       Values.Equals(stmt.Values);
            }
            return base.Equals(other);
        }

        public override bool Equals(SqlStatement obj)
        {
            if (obj is SqlInsertStatement item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}
