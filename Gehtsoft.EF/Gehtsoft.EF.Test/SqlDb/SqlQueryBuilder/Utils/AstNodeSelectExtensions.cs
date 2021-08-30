using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using Gehtsoft.EF.Test.SqlParser;

namespace Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder
{
    public static class AstNodeSelectExtensions
    {
        public static IEnumerable<IAstNode> SelectStatements(this IAstNode tree) => tree.Select("/SELECT");

        public static IAstNode SelectStatement(this IAstNode tree, int index = 0) => tree.Select("/SELECT").Skip(index).FirstOrDefault();

        public static IEnumerable<IAstNode> Resultset(this IAstNode select) => select.Select("/SELECT_LIST/SELECT_SUBLIST/EXPR_ALIAS");

        public static IAstNode ResultsetItem(this IAstNode select, int index) => select.Select("/SELECT_LIST/SELECT_SUBLIST/EXPR_ALIAS").Skip(index).FirstOrDefault();

        public static IAstNode ResultsetItemExpression(this IAstNode rsi) => rsi.SelectNode("/*", 1);

        public static IEnumerable<IAstNode> AllTables(this IAstNode select) => select.Select("/TABLE_EXPRESSION/FROM_CLAUSE/TABLE_REFERENCE_LIST/*");

        public static IAstNode Table(this IAstNode select, int index) => select.AllTables().Skip(index).FirstOrDefault();

        public static IAstNode Identifier(this IAstNode any, int index = 0) => any.Select("/IDENTIFIER").Skip(index).FirstOrDefault();

        public static IAstNode Field(this IAstNode any, int index = 0) => any.Select("/FIELD").Skip(index).FirstOrDefault();

        public static IAstNode TableName(this IAstNode table)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            if (table.Symbol == "TABLE_PRIMARY")
                return table.SelectNode("/TABLE_NAME/IDENTIFIER");
            else if (table.Symbol == "TABLE_REFERENCE")
                return table.SelectNode("/TABLE_PRIMARY/TABLE_NAME/IDENTIFIER");
            return null;
        }

        public static IAstNode TableAlias(this IAstNode table)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            if (table.Symbol == "TABLE_PRIMARY")
                return table.SelectNode("/IDENTIFIER");
            else if (table.Symbol == "TABLE_REFERENCE")
                return table.SelectNode("/TABLE_PRIMARY/IDENTIFIER");

            return null;
        }

        public static bool TableIsJoin(this IAstNode table) => table?.Symbol == "TABLE_REFERENCE" && (table?.Select("/JOIN_CONDITION")?.Any() ?? false);

        public static string TableJoinType(this IAstNode table)
        {
            if (TableIsJoin(table))
                return (table.SelectNode("/JOIN_TYPE/*", 1)?.Symbol) ?? "JOIN_TYPE_INNER";
            return null;
        }

        public static IAstNode TableJoinCondition(this IAstNode table)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            if (TableIsJoin(table))
                return table.SelectNode("/JOIN_CONDITION/*", 1);
            return null;
        }

        public static IAstNode ResultsetExpr(this IAstNode resultsetItem) => resultsetItem.SelectNode("/*", 1);

        public static string ResultsetItemAlias(this IAstNode resultsetItem) => resultsetItem.SelectNode("/IDENTIFIER", 1)?.Value;

        public static IEnumerable<IAstNode> SetQuantifiers(this IAstNode select) => select.Select("/SET_QUANTIFIER/*");

        public static IAstNode SelectWhere(this IAstNode select) => select.SelectNode("/TABLE_EXPRESSION/WHERE_CLAUSE");

        public static IAstNode SelectHaving(this IAstNode select) => select.SelectNode("/HAVING_CLAUSE");

        public static IAstNode ClauseCondition(this IAstNode clause) => clause.SelectNode("/*", 1);

        public static IAstNode SelectOffsetClause(this IAstNode select) => select.SelectNode("/LIMIT_OFFSET/OFFSET");

        public static IAstNode SelectLimitClause(this IAstNode select) => select.SelectNode("/LIMIT_OFFSET/LIMIT");

        public static int LimitOffsetValue(this IAstNode clause) => int.Parse(clause.SelectNode("/*", 1).Value, CultureInfo.InvariantCulture);

        public static IAstNode SelectSort(this IAstNode select) => select.SelectNode("/SORT_SPECIFICATION_LIST");

        public static IAstNode SelectGroupBy(this IAstNode select) => select.SelectNode("/GROUP_SPECIFICATION_LIST");

        public static IAstNode SortOrder(this IAstNode clause, int index) => clause.SelectNode("/SORT_SPECIFICATION", index + 1);

        public static IAstNode GroupOrder(this IAstNode clause, int index) => clause.SelectNode("/GROUP_SPECIFICATION", index + 1);

        public static IAstNode SortOrderExpr(this IAstNode order) => order.SelectNode("/*[1]");

        public static string SortOrderDirection(this IAstNode order) => order.SelectNode("/*[2]")?.Value ?? "ASC";
    }
}

