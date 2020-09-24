using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Xunit;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlStatement;

namespace Gehtsoft.EF.Db.SqlDb.Sql.Test
{
    public class DeclareCursorParse
    {
        private SqlCodeDomBuilder DomBuilder { get; }

        public DeclareCursorParse()
        {
            EntityFinder.EntityTypeInfo[] entities = EntityFinder.FindEntities(new Assembly[] { typeof(Snapshot).Assembly }, "northwind", false);
            DomBuilder = new SqlCodeDomBuilder();
            DomBuilder.Build(entities, "entities");
        }

        [Fact]
        public void DeclareOnly()
        {
            StatementSetEnvironment result = DomBuilder.Parse("test",
                "DECLARE my_cur CURSOR FOR " +
                "SELECT OrderID AS ID, Quantity, " +
                "Order.OrderDate, Customer.CompanyName, Employee.FirstName " +
                "FROM OrderDetail " +
                "AUTO JOIN Order " +
                "AUTO JOIN Customer " +
                "AUTO JOIN Employee "
                );

            SqlExpressionAliasCollection selectList = new SqlExpressionAliasCollection();
            SqlTableSpecificationCollection fromTables = new SqlTableSpecificationCollection();
            SqlSelectStatement select = new SqlSelectStatement(DomBuilder,
                new SqlSelectList(selectList),
                new SqlFromClause(fromTables)
            );

            fromTables.Add(
                new SqlAutoJoinedTable(
                    new SqlAutoJoinedTable(
                        new SqlAutoJoinedTable(
                            new SqlPrimaryTable(select, "OrderDetail"),
                            new SqlPrimaryTable(select, "Order")
                        ),
                        new SqlPrimaryTable(select, "Customer")
                    ),
                    new SqlPrimaryTable(select, "Employee")
                )
            );

            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "OrderID"), "ID"));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "Quantity")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "OrderDate", "Order")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "CompanyName", "Customer")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "FirstName", "Employee")));

            StatementSetEnvironment target = new StatementSetEnvironment() { new DeclareCursorStatement(DomBuilder, "?my_cur", select) };

            result.Equals(target).Should().BeTrue();
        }

        [Fact]
        public void DeclareOpenClose()
        {
            StatementSetEnvironment result = DomBuilder.Parse("test",
                "DECLARE my_cur CURSOR FOR " +
                "SELECT OrderID AS ID, Quantity, " +
                "Order.OrderDate, Customer.CompanyName, Employee.FirstName " +
                "FROM OrderDetail " +
                "AUTO JOIN Order " +
                "AUTO JOIN Customer " +
                "AUTO JOIN Employee " +
                "OPEN  CURSOR ?my_cur " +
                "CLOSE CURSOR ?my_cur "
            );

            SqlExpressionAliasCollection selectList = new SqlExpressionAliasCollection();
            SqlTableSpecificationCollection fromTables = new SqlTableSpecificationCollection();
            SqlSelectStatement select = new SqlSelectStatement(DomBuilder,
                new SqlSelectList(selectList),
                new SqlFromClause(fromTables)
            );

            fromTables.Add(
                new SqlAutoJoinedTable(
                    new SqlAutoJoinedTable(
                        new SqlAutoJoinedTable(
                            new SqlPrimaryTable(select, "OrderDetail"),
                            new SqlPrimaryTable(select, "Order")
                        ),
                        new SqlPrimaryTable(select, "Customer")
                    ),
                    new SqlPrimaryTable(select, "Employee")
                )
            );

            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "OrderID"), "ID"));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "Quantity")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "OrderDate", "Order")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "CompanyName", "Customer")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "FirstName", "Employee")));

            StatementSetEnvironment target = new StatementSetEnvironment() {
                new DeclareCursorStatement(DomBuilder, "?my_cur", select),
                new OpenCursorStatement(DomBuilder, new GlobalParameter("?my_cur", SqlBaseExpression.ResultTypes.Cursor)),
                new CloseCursorStatement(DomBuilder, new GlobalParameter("?my_cur", SqlBaseExpression.ResultTypes.Cursor))
            };

            result.Equals(target).Should().BeTrue();
        }

        [Fact]
        public void DeclareOpenFetchClose()
        {
            StatementSetEnvironment result = DomBuilder.Parse("test",
                "DECLARE my_cur CURSOR FOR " +
                "SELECT OrderID AS ID, Quantity, " +
                "Order.OrderDate, Customer.CompanyName, Employee.FirstName " +
                "FROM OrderDetail " +
                "AUTO JOIN Order " +
                "AUTO JOIN Customer " +
                "AUTO JOIN Employee " +
                "OPEN CURSOR ?my_cur " +
                "SET record = FETCH(?my_cur) " +
                "CLOSE CURSOR ?my_cur " +
                "EXIT WITH ?record"
            );

            SqlExpressionAliasCollection selectList = new SqlExpressionAliasCollection();
            SqlTableSpecificationCollection fromTables = new SqlTableSpecificationCollection();
            SqlSelectStatement select = new SqlSelectStatement(DomBuilder,
                new SqlSelectList(selectList),
                new SqlFromClause(fromTables)
            );

            fromTables.Add(
                new SqlAutoJoinedTable(
                    new SqlAutoJoinedTable(
                        new SqlAutoJoinedTable(
                            new SqlPrimaryTable(select, "OrderDetail"),
                            new SqlPrimaryTable(select, "Order")
                        ),
                        new SqlPrimaryTable(select, "Customer")
                    ),
                    new SqlPrimaryTable(select, "Employee")
                )
            );

            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "OrderID"), "ID"));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "Quantity")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "OrderDate", "Order")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "CompanyName", "Customer")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "FirstName", "Employee")));

            SetStatement set = new SetStatement(DomBuilder, new SetItemCollection()
            {
                new SetItem("record", new Fetch( new GlobalParameter("?my_cur", SqlBaseExpression.ResultTypes.Cursor)))
            });
            ExitStatement exit = new ExitStatement(DomBuilder, new GlobalParameter("?record", SqlBaseExpression.ResultTypes.Row));

            StatementSetEnvironment target = new StatementSetEnvironment() {
                new DeclareCursorStatement(DomBuilder, "?my_cur", select),
                new OpenCursorStatement(DomBuilder, new GlobalParameter("?my_cur", SqlBaseExpression.ResultTypes.Cursor)),
                set,
                new CloseCursorStatement(DomBuilder, new GlobalParameter("?my_cur", SqlBaseExpression.ResultTypes.Cursor)),
                exit
            };

            result.Equals(target).Should().BeTrue();
        }
    }
}
