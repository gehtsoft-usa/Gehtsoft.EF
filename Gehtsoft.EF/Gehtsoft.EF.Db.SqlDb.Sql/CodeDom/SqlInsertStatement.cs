using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using System.Linq.Expressions;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    /// <summary>
    /// Insert statement
    /// </summary>
    internal class SqlInsertStatement : SqlStatement
    {
        internal SqlFieldCollection Fields { get; } = null;
        internal SqlConstantCollection Values { get; } = null;
        internal string TableName { get; } = null;
        private EntityDescriptor mEntityDescriptor = null;
        internal SqlSelectStatement RightSelect { get; } = null;

        internal EntityDescriptor EntityDescriptor
        {
            get
            {
                return mEntityDescriptor ?? (mEntityDescriptor = this.EntityEntrys.Find(TableName).EntityDescriptor);
            }
        }

        internal SqlInsertStatement(SqlCodeDomBuilder builder, SqlParser.InsertStatementContext statementNode, string currentSource)
            : base(builder, StatementId.Insert, currentSource, statementNode.Line(), statementNode.Col())
        {
            TableName = statementNode.IDENTIFIER().GetText();
            try
            {
                this.AddEntityEntry(TableName, null);
            }
            catch
            {
                throw new SqlParserException(new SqlError(currentSource,
                    statementNode.Line(),
                    statementNode.Col(),
                    $"Not found entity with name '{TableName}'"));
            }

            Fields = new SqlFieldCollection();
            foreach (SqlParser.FieldContext fieldNode in statementNode.fieldsList().fields().field())
            {
                Fields.Add(new SqlField(this, fieldNode, currentSource));
            }

            SqlParser.ToInsertContext toInsert = statementNode.toInsert();
            if (toInsert.valuesList() != null)
            {
                Values = new SqlConstantCollection();
                foreach (SqlParser.ConstantContext constantNode in toInsert.valuesList().values().constant())
                {
                    Values.Add((SqlConstant)SqlExpressionParser.ParseConstant(constantNode, currentSource));
                }
            }
            else if (toInsert.selectStatement() != null)
            {
                SqlParser.SelectStatementContext selectNode = toInsert.selectStatement();
                RightSelect = new SqlSelectStatement(this.CodeDomBuilder, selectNode, currentSource);
                if (RightSelect.Grouping != null)
                {
                    throw new SqlParserException(new SqlError(currentSource,
                        selectNode.Line(),
                        selectNode.Col(),
                        "GROUP BY can not be used in SELECT part of INSERT operator"));
                }
                if (RightSelect.Sorting != null)
                {
                    throw new SqlParserException(new SqlError(currentSource,
                        selectNode.Line(),
                        selectNode.Col(),
                        "SORT BY can not be used in SELECT part of INSERT operator"));
                }
            }
            else
            {
                throw new SqlParserException(new SqlError(currentSource,
                    toInsert.Line(),
                    toInsert.Col(),
                    $"Unknown right part of INSERT ({toInsert.GetText()})"));
            }
            CheckFieldsAndValues();
        }

        internal override Expression ToLinqWxpression()
        {
            InsertRunner runner = new InsertRunner(CodeDomBuilder, CodeDomBuilder.Connection);
            return Expression.Call(Expression.Constant(runner), "RunWithResult", null, Expression.Constant(this));
        }

        internal protected void CheckFieldsAndValues()
        {
            if (Values != null)
            {
                if (Fields.Count != Values.Count)
                {
                    throw new SqlParserException(new SqlError(null, 0, 0, "Number of fields and values in INSERT statement should be the same"));
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
            }

            foreach (TableDescriptor.ColumnInfo column in EntityDescriptor.TableDescriptor)
            {
                if (Fields.FindByName(column.ID) == null)
                {
                    if (!column.Nullable && column.DefaultValue == null && !(column.PrimaryKey && column.Autoincrement))
                    {
                        throw new SqlParserException(new SqlError(null, 0, 0, $"Value for the field '{column.ID}' should be set"));
                    }
                }
            }
        }
    }
}
