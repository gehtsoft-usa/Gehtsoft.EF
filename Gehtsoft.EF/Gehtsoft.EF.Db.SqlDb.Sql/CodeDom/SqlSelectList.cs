using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class SqlSelectList : IEquatable<SqlSelectList>
    {
        public bool All { get; }
        public SqlExpressionAliasCollection FieldAliasCollection { get; } = null;

        internal SqlSelectList(SqlStatement parentStatement, ASTNode statementNode, string source)
        {
            if (statementNode.Children[0].Symbol.ID == SqlParser.ID.VariableAsrerisk)
                All = true;
            else if (statementNode.Children[0].Symbol.ID == SqlParser.ID.VariableSelectSublist)
            {
                All = false;
                FieldAliasCollection = new SqlExpressionAliasCollection();
                ASTNode expressionAliasCollectionNode = statementNode.Children[0];
                foreach (ASTNode expressionAliasNode in expressionAliasCollectionNode.Children)
                {
                    FieldAliasCollection.Add(new SqlExpressionAlias(parentStatement, expressionAliasNode, source));
                }
            }
            else
            {
                throw new SqlParserException(new SqlError(source,
                    statementNode.Position.Line,
                    statementNode.Position.Column,
                    $"Unexpected or incorrect node {statementNode.Symbol.Name}({statementNode.Value ?? "null"})"));
            }
        }

        internal SqlSelectList()
        {
            All = true;
        }

        internal SqlSelectList(SqlExpressionAliasCollection fieldAliasCollection)
        {
            All = false;
            FieldAliasCollection = fieldAliasCollection;
        }


        public virtual bool Equals(SqlSelectList other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            if (this.All != other.All)
                return false;
            if (this.FieldAliasCollection != null && other.FieldAliasCollection != null)
            {
                if (this.FieldAliasCollection.Count != other.FieldAliasCollection.Count)
                    return false;

                foreach(SqlExpressionAlias thisFld in this.FieldAliasCollection)
                {
                    bool found = false;
                    foreach (SqlExpressionAlias otherFld in other.FieldAliasCollection)
                    {
                        if(thisFld.Equals(otherFld))
                        {
                            found = true;
                            break;
                        }
                    }
                    if(!found)
                        return false;
                }
                return true;
            }
            return this.FieldAliasCollection == null && other.FieldAliasCollection == null;
        }

        public override bool Equals(object obj)
        {
            if (obj is SqlSelectList item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
