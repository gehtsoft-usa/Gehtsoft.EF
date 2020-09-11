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
    public class ExitParse
    {
        private SqlCodeDomBuilder DomBuilder { get; }

        public ExitParse()
        {
            EntityFinder.EntityTypeInfo[] entities = EntityFinder.FindEntities(new Assembly[] { typeof(Snapshot).Assembly }, "northwind", false);
            DomBuilder = new SqlCodeDomBuilder();
            DomBuilder.Build(entities, "entities");
        }
        [Fact]
        public void ExitParse1()
        {
            SqlCodeDomBuilder environment = DomBuilder.NewEnvironment();
            StatementSetEnvironment result = environment.Parse("test",
                "DECLARE qqq AS INTEGER;" +
                "SET qqq = ROWS_COUNT(LAST_RESULT());" +
                "EXIT WITH ?qqq"
            );

            SetStatement set = new SetStatement(DomBuilder, new SetItemCollection()
            {
                new SetItem("qqq", new GetRowsCount(new GetLastResult()))
            });

            ExitStatement exit = new ExitStatement(environment, new GlobalParameter("?qqq", SqlBaseExpression.ResultTypes.Integer));

            StatementSetEnvironment target = new StatementSetEnvironment() { set, exit };

            result.Equals(target).Should().BeTrue();
        }

        [Fact]
        public void ExitParseError()
        {
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                "DECLARE qqq AS INTEGER;" +
                "SET qqq = ROWS_COUNT(LAST_RESULT());" +
                "EXIT WITH Low"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                "DECLARE qqq AS INTEGER;" +
                "SET qqq = ROWS_COUNT(UPPER('sss'));" +
                "EXIT WITH ?qqq"
                )
            );
        }
    }
}
