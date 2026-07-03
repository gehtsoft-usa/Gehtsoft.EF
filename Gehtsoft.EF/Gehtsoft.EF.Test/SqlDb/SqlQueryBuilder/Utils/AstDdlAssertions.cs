using AwesomeAssertions;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.SqlParser;

namespace Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder
{
    /// <summary>
    /// Semantic, composable assertions for DDL statements. Each expresses one piece of test
    /// intent and hides how the parse tree is navigated. A "HaveDropX" narrows the assertion
    /// subject to that statement node, so follow-ups (e.g. <see cref="HaveIfExists"/>) chain via
    /// <c>.And</c>. Bodies currently walk the tree via the path engine; when the parser backend
    /// changes, only these bodies change — call-sites stay the same.
    /// </summary>
    public static class AstDdlAssertions
    {
        public static AndConstraint<AstNodeAssertions> HaveDropTable(this AstNodeAssertions assertions, string tableName)
        {
            var node = assertions.Subject.SelectNode("/DROP_TABLE[1]");
            node.Should().Exist();
            node.SelectNode("/TABLE_NAME/IDENTIFIER").Should().HaveValue(tableName);
            return new AndConstraint<AstNodeAssertions>(node.Should());
        }

        public static AndConstraint<AstNodeAssertions> HaveDropView(this AstNodeAssertions assertions, string viewName)
        {
            var node = assertions.Subject.SelectNode("/DROP_VIEW[1]");
            node.Should().Exist();
            node.SelectNode("/TABLE_NAME/IDENTIFIER").Should().HaveValue(viewName);
            return new AndConstraint<AstNodeAssertions>(node.Should());
        }

        public static AndConstraint<AstNodeAssertions> HaveDropIndex(this AstNodeAssertions assertions, string indexFullName, string onTable)
        {
            var node = assertions.Subject.SelectNode("/DROP_INDEX[1]");
            node.Should().Exist();
            node.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue(indexFullName);
            node.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue(onTable);
            return new AndConstraint<AstNodeAssertions>(node.Should());
        }

        /// <summary>Asserts the current statement node carries an IF EXISTS clause.</summary>
        public static AndConstraint<AstNodeAssertions> HaveIfExists(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectNode("/IF_EXIST").Should().Exist();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        // ---- CREATE INDEX (subject narrows to the CREATE INDEX statement) ----

        public static AndConstraint<AstNodeAssertions> HaveCreateIndexCount(this AstNodeAssertions assertions, int count)
        {
            assertions.Subject.Select("/CREATE_INDEX").Should().HaveCount(count);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveCreateIndex(this AstNodeAssertions assertions, string indexName, string onTable, int position = 1)
        {
            var node = assertions.Subject.SelectNode($"/CREATE_INDEX[{position}]");
            node.Should().Exist();
            node.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue(indexName);
            node.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue(onTable);
            return new AndConstraint<AstNodeAssertions>(node.Should());
        }

        /// <summary>Asserts the position-th (1-based) index column is a field, with an optional sort direction.</summary>
        public static AndConstraint<AstNodeAssertions> HaveIndexColumn(this AstNodeAssertions assertions, int position, string field, SortDir? direction = null)
        {
            var spec = assertions.Subject.SelectNode($"//SORT_SPECIFICATION[{position}]");
            spec.Should().Exist();
            spec.SelectNode("/FIELD/IDENTIFIER").Should().HaveValue(field);
            if (direction != null)
                spec.Select("/DESC").Should().HaveCount(direction == SortDir.Desc ? 1 : 0);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        /// <summary>Asserts the position-th (1-based) index column is a function call over a field.</summary>
        public static AndConstraint<AstNodeAssertions> HaveIndexFunctionColumn(this AstNodeAssertions assertions, int position, string function, string field)
        {
            var spec = assertions.Subject.SelectNode($"//SORT_SPECIFICATION[{position}]");
            spec.Should().Exist();
            spec.SelectNode($"/*_CALL/{function}").Should().Exist();
            spec.SelectNode("/*_CALL/*[2]/IDENTIFIER").Should().HaveValue(field);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        // ---- CREATE TABLE (subject narrows to the CREATE TABLE statement) ----

        public static AndConstraint<AstNodeAssertions> HaveCreateTable(this AstNodeAssertions assertions, string tableName)
        {
            var node = assertions.Subject.SelectNode("/CREATE_TABLE[1]");
            node.Should().Exist();
            node.SelectNode("/TABLE_NAME/IDENTIFIER").Should().HaveValue(tableName);
            return new AndConstraint<AstNodeAssertions>(node.Should());
        }

        public static AndConstraint<AstNodeAssertions> HaveColumnCount(this AstNodeAssertions assertions, int count)
        {
            assertions.Subject.Select("//FIELD_DEFINITION").Should().HaveCount(count);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveForeignKeyCount(this AstNodeAssertions assertions, int count)
        {
            assertions.Subject.Select("//FOREIGN_KEY_DEFINITION").Should().HaveCount(count);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        /// <summary>Asserts the position-th (1-based) column definition; narrows the subject to it for flag chaining.</summary>
        public static AndConstraint<AstNodeAssertions> HaveColumn(this AstNodeAssertions assertions, int position, string name, string type, int? size = null, int? precision = null)
        {
            // SelectNode's index parameter flattens all recursive matches then picks the Nth
            // (a path "[N]" index counts per-recursion-level, which is wrong across clauses).
            var col = assertions.Subject.SelectNode("//FIELD_DEFINITION", position);
            col.Should().Exist();
            col.SelectNode("/*_NAME/*[1]").Should().HaveValue(name);
            col.SelectNode("/*_TYPE/*[1]").Should().HaveValue(type);
            if (size != null)
                col.SelectNode("/*_TYPE/*_SIZE/INT[1]").Should().HaveValue(size.Value.ToString());
            if (precision != null)
                col.SelectNode("/*_TYPE/*_SIZE/INT[2]").Should().HaveValue(precision.Value.ToString());
            return new AndConstraint<AstNodeAssertions>(col.Should());
        }

        // ---- column flags (subject is a column definition) ----

        public static AndConstraint<AstNodeAssertions> BePrimaryKey(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectNode("//*_PRIMARY_KEY").Should().Exist();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> BeAutoincrement(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectNode("//*_AUTOINCREMENT").Should().Exist();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> BeNotNull(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectNode("//*_NOT_NULL").Should().Exist();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> BeNullable(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectNode("//*_NOT_NULL").Should().NotExist();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> BeUnique(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectNode("//*_UNIQUE").Should().Exist();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveDefault(this AstNodeAssertions assertions, string value)
        {
            assertions.Subject.SelectNode("//*_DEFAULT").Should().Exist();
            assertions.Subject.SelectNode("//*_DEFAULT/*[1]").Should().HaveValue(value);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        /// <summary>Asserts a table-level FOREIGN KEY definition (field REFERENCES refTable(refColumn)).</summary>
        public static AndConstraint<AstNodeAssertions> HaveForeignKey(this AstNodeAssertions assertions, string field, string refTable, string refColumn)
        {
            var fk = assertions.Subject.SelectNode("//FOREIGN_KEY_DEFINITION");
            fk.Should().Exist();
            fk.SelectNode("/FIELD_*_NAME[1]/IDENTIFIER").Should().HaveValue(field);
            fk.SelectNode("/TABLE_NAME/IDENTIFIER").Should().HaveValue(refTable);
            fk.SelectNode("/FIELD_*_NAME[2]/IDENTIFIER").Should().HaveValue(refColumn);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        // ---- ALTER TABLE (subject narrows to the ALTER TABLE statement) ----

        public static AndConstraint<AstNodeAssertions> HaveAlterTable(this AstNodeAssertions assertions, string tableName)
        {
            var node = assertions.Subject.SelectNode("/ALTER_TABLE[1]");
            node.Should().Exist();
            node.SelectNode("/TABLE_NAME/IDENTIFIER").Should().HaveValue(tableName);
            return new AndConstraint<AstNodeAssertions>(node.Should());
        }

        public static AndConstraint<AstNodeAssertions> HaveDropColumn(this AstNodeAssertions assertions, string name)
        {
            assertions.Subject.SelectNode("//DROP_FIELD_CLAUSE/FIELD_DEFINITION_NAME/IDENTIFIER").Should().HaveValue(name);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        // ---- CREATE VIEW ----

        public static AndConstraint<AstNodeAssertions> HaveCreateView(this AstNodeAssertions assertions, string viewName)
        {
            var node = assertions.Subject.SelectNode("/CREATE_VIEW[1]");
            node.Should().Exist();
            node.SelectNode("/TABLE_NAME/IDENTIFIER").Should().HaveValue(viewName);
            return new AndConstraint<AstNodeAssertions>(node.Should());
        }

        /// <summary>The SELECT statement inside a CREATE VIEW (or anywhere under the subject).</summary>
        public static IAstNode ViewSelect(this IAstNode ast) => ast.SelectNode("//SELECT");
    }
}
