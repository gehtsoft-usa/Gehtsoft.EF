using Hime.Redist;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    /// <summary>
    public class SqlUpdateAssign : IEquatable<SqlUpdateAssign>
    {
        public SqlField Field { get; } = null;
        public SqlSelectStatement Select { get; } = null;
        public SqlBaseExpression Expression { get; } = null;

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
        public virtual bool Equals(SqlUpdateAssign other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            if (!(this.Expression == null ? (other.Expression == null) : Expression.Equals(other.Expression)))
                return false;
            return this.Select == null ? (other.Select == null) : this.Select.Equals(other.Select);
        }

        public override bool Equals(object obj)
        {
            if (obj is SqlUpdateAssign item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    [Serializable]
    public class SqlUpdateAssignCollection : IReadOnlyList<SqlUpdateAssign>, IEquatable<SqlUpdateAssignCollection>
    {
        private readonly List<SqlUpdateAssign> mList = new List<SqlUpdateAssign>();

        internal SqlUpdateAssignCollection()
        {

        }

        public SqlUpdateAssign this[int index] => ((IReadOnlyList<SqlUpdateAssign>)mList)[index];

        public int Count => ((IReadOnlyCollection<SqlUpdateAssign>)mList).Count;

        public SqlUpdateAssign FindByFieldName(string name) => mList.Where(t => t.Field.FieldName == name).SingleOrDefault();

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

        public virtual bool Equals(SqlUpdateAssignCollection other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            if (this.Count != other.Count)
                return false;
            for (int i = 0; i < Count; i++)
            {
                if (!this[i].Equals(other[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is SqlUpdateAssignCollection item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
