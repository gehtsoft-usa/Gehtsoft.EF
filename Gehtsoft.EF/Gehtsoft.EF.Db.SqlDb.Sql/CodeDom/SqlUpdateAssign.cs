using Hime.Redist;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    /// <summary>
    internal class SqlUpdateAssign
    {
        internal SqlField Field { get; } = null;
        internal SqlSelectStatement Select { get; } = null;
        internal SqlBaseExpression Expression { get; } = null;

        internal SqlUpdateAssign(SqlStatement parentStatement, ASTNode updateAssignNode, string source)
        {
            Field = new SqlField(parentStatement, updateAssignNode.Children[0], source);
            ASTNode operand = updateAssignNode.Children[1];

            if (operand.Symbol.ID == SqlParser.ID.VariableSelect)
            {
                Select = new SqlSelectStatement(parentStatement.CodeDomBuilder, operand, source);
                if (Select.SelectList.FieldAliasCollection.Count != 1)
                {
                    throw new SqlParserException(new SqlError(source,
                        operand.Position.Line,
                        operand.Position.Column,
                        $"Expected 1 column in inner SELECT {operand.Symbol.Name} ({operand.Value ?? "null"})"));
                }

                //SqlUpdateStatement parent = (SqlUpdateStatement)parentStatement;
                //foreach (SqlTableSpecification currentTable in Select.FromClause.TableCollection)
                //{
                //    if (tableInUse(currentTable, parent.TableName))
                //    {
                //        throw new SqlParserException(new SqlError(source,
                //            operand.Position.Line,
                //            operand.Position.Column,
                //            $"Table '{parent.TableName}' is used both in UPDATE and in inner SELECT"));
                //    }
                //}
            }
            else
            {
                Expression = SqlExpressionParser.ParseExpression(parentStatement, operand, source);
            }
        }

        //private bool tableInUse(SqlTableSpecification table, string tableName)
        //{
        //    bool retval = false;

        //    if (table is SqlPrimaryTable primaryTable)
        //    {
        //        retval = primaryTable.TableName == tableName;
        //    }
        //    else if (table is SqlQualifiedJoinedTable joinedTable)
        //    {
        //        retval = tableInUse(joinedTable.LeftTable, tableName);
        //    }
        //    return retval;
        //}

        internal SqlUpdateAssign(SqlField field, SqlBaseExpression expression)
        {
            Field = field;
            Expression = expression;
        }
        internal SqlUpdateAssign(SqlField field, SqlSelectStatement select)
        {
            Field = field;
            Select = select;
        }
    }

    [Serializable]
    internal class SqlUpdateAssignCollection : IReadOnlyList<SqlUpdateAssign>
    {
        private readonly List<SqlUpdateAssign> mList = new List<SqlUpdateAssign>();

        internal SqlUpdateAssignCollection()
        {

        }

        public SqlUpdateAssign this[int index] => ((IReadOnlyList<SqlUpdateAssign>)mList)[index];

        public int Count => ((IReadOnlyCollection<SqlUpdateAssign>)mList).Count;

        internal SqlUpdateAssign FindByFieldName(string name) => mList.Where(t => t.Field.FieldName == name).SingleOrDefault();

        public IEnumerator<SqlUpdateAssign> GetEnumerator()
        {
            return ((IEnumerable<SqlUpdateAssign>)mList).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mList).GetEnumerator();
        }

        internal void Add(SqlUpdateAssign updateAssign)
        {
            mList.Add(updateAssign);
        }
    }
}
