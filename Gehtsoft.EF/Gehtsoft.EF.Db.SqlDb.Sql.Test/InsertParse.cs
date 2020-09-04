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
    public class InsertParse
    {
        private SqlCodeDomBuilder DomBuilder { get; }

        public InsertParse()
        {
            EntityFinder.EntityTypeInfo[] entities = EntityFinder.FindEntities(new Assembly[] { typeof(Snapshot).Assembly }, "northwind", false);
            DomBuilder = new SqlCodeDomBuilder();
            DomBuilder.Build(entities, "entities");
        }

        [Fact]
        public void InsertSimple()
        {
            SqlStatementCollection result = DomBuilder.Parse("test",
                "INSERT INTO Supplier "+
                "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax, HomePage) " +
                "VALUES " +
                "('Gehtsoft', 'Just Gehtsoft', 'Wow', '1-st street 1', 'Omsk', 'Siberia', '644000', 'Russia', '123456789', '123456789', 't.com')"
            );

            SqlFieldCollection fields = new SqlFieldCollection();
            SqlConstantCollection values = new SqlConstantCollection()
            {
                new SqlConstant("Gehtsoft", SqlBaseExpression.ResultTypes.String),
                new SqlConstant("Just Gehtsoft", SqlBaseExpression.ResultTypes.String),
                new SqlConstant("Wow", SqlBaseExpression.ResultTypes.String),
                new SqlConstant("1-st street 1", SqlBaseExpression.ResultTypes.String),
                new SqlConstant("Omsk", SqlBaseExpression.ResultTypes.String),
                new SqlConstant("Siberia", SqlBaseExpression.ResultTypes.String),
                new SqlConstant("644000", SqlBaseExpression.ResultTypes.String),
                new SqlConstant("Russia", SqlBaseExpression.ResultTypes.String),
                new SqlConstant("123456789", SqlBaseExpression.ResultTypes.String),
                new SqlConstant("123456789", SqlBaseExpression.ResultTypes.String),
                new SqlConstant("t.com", SqlBaseExpression.ResultTypes.String),
            };

            SqlInsertStatement insert = new SqlInsertStatement(DomBuilder, "Supplier", fields, values);

            fields.Add(new SqlField(insert, "CompanyName"));
            fields.Add(new SqlField(insert, "ContactName"));
            fields.Add(new SqlField(insert, "ContactTitle"));
            fields.Add(new SqlField(insert, "Address"));
            fields.Add(new SqlField(insert, "City"));
            fields.Add(new SqlField(insert, "Region"));
            fields.Add(new SqlField(insert, "PostalCode"));
            fields.Add(new SqlField(insert, "Country"));
            fields.Add(new SqlField(insert, "Phone"));
            fields.Add(new SqlField(insert, "Fax"));
            fields.Add(new SqlField(insert, "HomePage"));

            insert.CheckFieldsAndValues();

            SqlStatementCollection target = new SqlStatementCollection() { insert };

            result.Equals(target).Should().BeTrue();
        }

        [Fact]
        public void InsertParseError()
        {
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                    "INSERT INTO Supplier " +
                    "(CompanyNameQQQ, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax, HomePage) " +
                    "VALUES " +
                    "('Gehtsoft', 'Just Gehtsoft', 'Wow', '1-st street 1', 'Omsk', 'Siberia', '644000', 'Russia', '123456789', '123456789', 't.com')"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                    "INSERT INTO Supplier " +
                    "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax, HomePage) " +
                    "VALUES " +
                    "(123, 'Just Gehtsoft', 'Wow', '1-st street 1', 'Omsk', 'Siberia', '644000', 'Russia', '123456789', '123456789', 't.com')"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                    "INSERT INTO Supplier " +
                    "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax, HomePage) " +
                    "VALUES " +
                    "(NULL, 'Just Gehtsoft', 'Wow', '1-st street 1', 'Omsk', 'Siberia', '644000', 'Russia', '123456789', '123456789', 't.com')"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                    "INSERT INTO Supplier " +
                    "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax, HomePage) " +
                    "VALUES " +
                    "('Gehtsoft', 'Just Gehtsoft', 'Wow', '1-st street 1', 'Omsk', 'Siberia', '644000')"
                )
            );
        }

        [Fact]
        public void InsertSelect()
        {
            SqlStatementCollection result = DomBuilder.Parse("test",
                "INSERT INTO Supplier " +
                "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax) " +
                "SELECT " +
                "CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax " +
                "FROM Customer WHERE PostalCode LIKE '80%'"
            );

            SqlExpressionAliasCollection selectList = new SqlExpressionAliasCollection();
            SqlFieldCollection fields = new SqlFieldCollection();
            SqlTableSpecificationCollection fromTables = new SqlTableSpecificationCollection();
            SqlSelectStatement select = new SqlSelectStatement(DomBuilder,
                new SqlSelectList(selectList),
                new SqlFromClause(fromTables),
                new SqlWhereClause()
                );

            fromTables.Add(new SqlPrimaryTable(select, "Customer"));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "CompanyName")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "ContactName")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "ContactTitle")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "Address")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "City")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "Region")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "PostalCode")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "Country")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "Phone")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "Fax")));

            select.WhereClause.RootExpression = new SqlCallFuncExpression(
                SqlBaseExpression.ResultTypes.Boolean, "LIKE",
                new SqlBaseExpressionCollection()
                {
                    new SqlField(select, "PostalCode"),
                    new SqlConstant("80%", SqlBaseExpression.ResultTypes.String)
                });

            SqlInsertStatement insert = new SqlInsertStatement(DomBuilder, "Supplier", fields, select);

            fields.Add(new SqlField(insert, "CompanyName"));
            fields.Add(new SqlField(insert, "ContactName"));
            fields.Add(new SqlField(insert, "ContactTitle"));
            fields.Add(new SqlField(insert, "Address"));
            fields.Add(new SqlField(insert, "City"));
            fields.Add(new SqlField(insert, "Region"));
            fields.Add(new SqlField(insert, "PostalCode"));
            fields.Add(new SqlField(insert, "Country"));
            fields.Add(new SqlField(insert, "Phone"));
            fields.Add(new SqlField(insert, "Fax"));

            insert.CheckFieldsAndValues();

            SqlStatementCollection target = new SqlStatementCollection() { insert };

            result.Equals(target).Should().BeTrue();
        }
    }
}
