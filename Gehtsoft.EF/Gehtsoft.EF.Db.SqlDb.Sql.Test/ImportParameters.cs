using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Linq;
using System.Text;
using Xunit;
using System.Linq.Expressions;

namespace Gehtsoft.EF.Db.SqlDb.Sql.Test
{
    public class ImportParameters
    {
        private SqlCodeDomBuilder DomBuilder { get; }

        public ImportParameters()
        {
            DomBuilder = new SqlCodeDomBuilder();
        }

        [Fact]
        public void Success()
        {
            object result;
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment();

            var calcFactorial = environment.Parse("test",
                "IMPORT param AS INTEGER, text AS STRING;" +
                "SET factorial = 1;" +
                "FOR ?n := 0 WHILE ?n <= ?param NEXT ?n := ?n+1 LOOP " +
                "   IF ?n = 0 THEN CONTINUE; END IF;" +
                "   ?factorial := ?factorial * ?n;" +
                "END LOOP;" +
                "EXIT WITH ?text || '_' || TOSTRING(?factorial)"
            );
            result = calcFactorial(new Dictionary<string, object>() { { "param", 6 }, { "text", "just" } });
            ((string)result).Should().Be("just_720");
        }

        [Fact]
        public void Error()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment();
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "IMPORT param AS INTEGER;"
                )(null)
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "IMPORT param AS INTEGER;"
                )(new Dictionary<string, object>() { { "qqq", 6 } })
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "IMPORT param AS INTEGER;"
                )(new Dictionary<string, object>() { { "param", "qqq" } })
            );
        }
    }
}
