using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlFromClause : IEquatable<SqlFromClause>
    {
        internal SqlTableSpecificationCollection TableCollection { get; } = null;
        internal SqlFromClause(SqlStatement parentStatement, ASTNode statementNode, string source)
        {
            if (statementNode.Children[0].Symbol.ID == SqlParser.ID.VariableTableReferenceList)
            {
                TableCollection = new SqlTableSpecificationCollection();
                ASTNode tableReferenceCollectionNode = statementNode.Children[0];
                foreach (ASTNode tableReferenceNode in tableReferenceCollectionNode.Children)
                {
                    if (tableReferenceNode.Symbol.ID == SqlParser.ID.VariableTablePrimary)
                    {
                        TableCollection.Add(new SqlPrimaryTable(parentStatement, tableReferenceNode, source));
                    }
                    else if (tableReferenceNode.Symbol.ID == SqlParser.ID.VariableQualifiedJoin)
                    {
                        TableCollection.Add(new SqlQualifiedJoinedTable(parentStatement, tableReferenceNode, source));
                    }
                    else if (tableReferenceNode.Symbol.ID == SqlParser.ID.VariableAutoJoin)
                    {
                        TableCollection.Add(new SqlAutoJoinedTable(parentStatement, tableReferenceNode, source));
                    }
                    else
                    {
                        throw new SqlParserException(new SqlError(source,
                            tableReferenceCollectionNode.Position.Line,
                            tableReferenceCollectionNode.Position.Column,
                            $"Unexpected table reference node {statementNode.Symbol.Name}({tableReferenceCollectionNode.Value ?? "null"})"));
                    }
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

        internal SqlFromClause(SqlTableSpecificationCollection tableCollection)
        {
            TableCollection = tableCollection;
        }

        bool IEquatable<SqlFromClause>.Equals(SqlFromClause other) => Equals(other);
        internal virtual bool Equals(SqlFromClause other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            if (this.TableCollection != null && other.TableCollection != null)
            {
                if (this.TableCollection.Count != other.TableCollection.Count)
                    return false;

                foreach (SqlTableSpecification thisTbl in this.TableCollection)
                {
                    bool found = false;
                    foreach (SqlTableSpecification otherTbl in other.TableCollection)
                    {
                        if (thisTbl.Equals(otherTbl))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        return false;
                }
                return true;
            }
            return this.TableCollection == null && other.TableCollection == null;
        }

        public override bool Equals(object obj)
        {
            if (obj is SqlFromClause item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
