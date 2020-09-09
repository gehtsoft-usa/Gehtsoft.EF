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
    public class SetParse
    {
        private SqlCodeDomBuilder DomBuilder { get; }

        public SetParse()
        {
            EntityFinder.EntityTypeInfo[] entities = EntityFinder.FindEntities(new Assembly[] { typeof(Snapshot).Assembly }, "northwind", false);
            DomBuilder = new SqlCodeDomBuilder();
            DomBuilder.Build(entities, "entities");
        }

        [Fact]
        public void SetParse1()
        {
            StatementCollection result = DomBuilder.Parse("test",
                "SET qqq = 'Con' || '%'"
            );

            SetStatement set = new SetStatement(DomBuilder, new SetItemCollection()
            {
                new SetItem("qqq", new SqlConstant("Con%", SqlBaseExpression.ResultTypes.String))
            });

            StatementCollection target = new StatementCollection() { set };

            result.Equals(target).Should().BeTrue();
        }

        [Fact]
        public void SetParse2()
        {
            StatementCollection result = DomBuilder.Parse("test",
                "SET qqq = UPPER(?mmm AS STRING) = 'WWWWW'"
            );

            SetStatement set = new SetStatement(DomBuilder, new SetItemCollection()
            {
                new SetItem("qqq", new SqlBinaryExpression(
                        new SqlCallFuncExpression(SqlBaseExpression.ResultTypes.String, "UPPER",
                            new SqlBaseExpressionCollection() { new GlobalParameter("?mmm", SqlBaseExpression.ResultTypes.String) }),
                        SqlBinaryExpression.OperationType.Eq,
                        new SqlConstant("WWWWW", SqlBaseExpression.ResultTypes.String)
                    ))
            });

            StatementCollection target = new StatementCollection() { set };

            result.Equals(target).Should().BeTrue();
        }

        [Fact]
        public void SetPars3()
        {
            StatementCollection result = DomBuilder.Parse("test",
                "DECLARE qqq AS BOOLEAN, mmm AS STRING;" +
                "SET qqq = UPPER(?mmm) = 'WWWWW';"
            );

            SetStatement set = new SetStatement(DomBuilder, new SetItemCollection()
            {
                new SetItem("qqq", new SqlBinaryExpression(
                        new SqlCallFuncExpression(SqlBaseExpression.ResultTypes.String, "UPPER",
                            new SqlBaseExpressionCollection() { new GlobalParameter("?mmm", SqlBaseExpression.ResultTypes.String) }),
                        SqlBinaryExpression.OperationType.Eq,
                        new SqlConstant("WWWWW", SqlBaseExpression.ResultTypes.String)
                    ))
            });

            StatementCollection target = new StatementCollection() { set };

            result.Equals(target).Should().BeTrue();
        }

        [Fact]
        public void SetParseError()
        {
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                "SET qqq = UPPER(field) = 'WWWWW'"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                "SET qqq = COUNT(*) + 1"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                "SET qqq = UPPER(?mmm AS INTEGER) = 'WWWWW'"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                "SET qqq = UPPER(?mmm) = 'WWWWW'"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                "DECLARE qqq AS STRING, mmm AS STRING;" +
                "SET qqq = UPPER(?mmm) = 'WWWWW';"
                )
            );
        }
    }
}
