using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using Gehtsoft.EF.Test.SqlParser;

namespace Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder
{
    /// <summary>
    /// Semantic, composable assertions and navigation for DML statements (INSERT / UPDATE / DELETE).
    /// Each method expresses one piece of test intent and hides how the parse tree is navigated;
    /// a "HaveXxx" narrows the assertion subject to that statement node so follow-ups chain via
    /// <c>.And</c>. Expression/SELECT sub-trees are validated with the shared vocabulary
    /// (BeFieldExpression / BeParamExpression / BeOpExpression / ItsParameter / BeConstant /
    /// Table / Resultset / SelectWhere / ClauseCondition). Bodies currently walk the tree via the
    /// path engine; when the parser backend changes, only these bodies change — call-sites stay put.
    /// </summary>
    public static class AstDmlAssertions
    {
        // ---- navigation ------------------------------------------------------

        public static IAstNode InsertStatement(this IAstNode ast) => ast.SelectNode("/INSERT");
        public static IAstNode UpdateStatement(this IAstNode ast) => ast.SelectNode("/UPDATE");
        public static IAstNode DeleteStatement(this IAstNode ast) => ast.SelectNode("/DELETE");

        /// <summary>The SELECT feeding an INSERT ... SELECT (null for INSERT ... VALUES).</summary>
        public static IAstNode InsertSelect(this IAstNode insert) => insert.SelectNode("/SELECT");

        public static IEnumerable<IAstNode> UpdateAssigns(this IAstNode update) => update.Select("/UPDATE_LIST/UPDATE_ASSIGN");
        public static IAstNode UpdateAssign(this IAstNode update, int index) => update.UpdateAssigns().Skip(index).FirstOrDefault();

        /// <summary>The target column of an UPDATE assignment.</summary>
        public static IAstNode AssignTarget(this IAstNode assign) => assign.SelectNode("/FIELD");

        /// <summary>The assigned value of an UPDATE assignment (a param, an expression, or a sub-SELECT).</summary>
        public static IAstNode AssignValue(this IAstNode assign) => assign.SelectNode("/*[2]");

        /// <summary>The boolean condition of a statement-level WHERE clause (UPDATE / DELETE), or null.</summary>
        public static IAstNode WhereCondition(this IAstNode statement) => statement.SelectNode("/WHERE_CLAUSE")?.ClauseCondition();

        // ---- INSERT (subject narrows to the INSERT statement) ----------------

        public static AndConstraint<AstNodeAssertions> HaveInsert(this AstNodeAssertions assertions, string tableName)
        {
            var node = assertions.Subject.SelectNode("/INSERT");
            node.Should().Exist();
            node.SelectNode("/TABLE_NAME/IDENTIFIER").Should().HaveValue(tableName);
            return new AndConstraint<AstNodeAssertions>(node.Should());
        }

        /// <summary>Asserts the INSERT column list, in order.</summary>
        public static AndConstraint<AstNodeAssertions> HaveInsertFields(this AstNodeAssertions assertions, params string[] names)
        {
            var fields = assertions.Subject.Select("/FIELDS/FIELD").ToArray();
            fields.Should().HaveCount(names.Length);
            for (int i = 0; i < names.Length; i++)
                fields[i].ExprFieldName().Should().Be(names[i]);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        /// <summary>Asserts the INSERT ... VALUES parameters, in order (each value is a bound parameter).</summary>
        public static AndConstraint<AstNodeAssertions> HaveInsertValues(this AstNodeAssertions assertions, params string[] paramNames)
        {
            var values = assertions.Subject.Select("/INSERT_VALUES_LIST/INSERT_VALUES/INSERT_VALUE/PARAM").ToArray();
            values.Should().HaveCount(paramNames.Length);
            for (int i = 0; i < paramNames.Length; i++)
                values[i].ExprParamName().Should().Be(paramNames[i]);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        // ---- UPDATE (subject narrows to the UPDATE statement) ----------------

        public static AndConstraint<AstNodeAssertions> HaveUpdate(this AstNodeAssertions assertions, string tableName)
        {
            var node = assertions.Subject.SelectNode("/UPDATE");
            node.Should().Exist();
            node.SelectNode("/TABLE_NAME/IDENTIFIER").Should().HaveValue(tableName);
            return new AndConstraint<AstNodeAssertions>(node.Should());
        }

        public static AndConstraint<AstNodeAssertions> HaveUpdateAssignCount(this AstNodeAssertions assertions, int count)
        {
            assertions.Subject.UpdateAssigns().Should().HaveCount(count);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        // ---- DELETE (subject narrows to the DELETE statement) ----------------

        public static AndConstraint<AstNodeAssertions> HaveDelete(this AstNodeAssertions assertions, string tableName)
        {
            var node = assertions.Subject.SelectNode("/DELETE");
            node.Should().Exist();
            node.SelectNode("/TABLE_NAME/IDENTIFIER").Should().HaveValue(tableName);
            return new AndConstraint<AstNodeAssertions>(node.Should());
        }

        /// <summary>Asserts the current statement (UPDATE / DELETE) carries no WHERE clause.</summary>
        public static AndConstraint<AstNodeAssertions> HaveNoWhere(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectNode("/WHERE_CLAUSE").Should().NotExist();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }
    }
}
