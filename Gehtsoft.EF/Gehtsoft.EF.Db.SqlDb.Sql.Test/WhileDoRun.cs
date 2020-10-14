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
    public class WhileDoRun
    {
        private SqlCodeDomBuilder DomBuilder { get; }

        public WhileDoRun()
        {
            DomBuilder = new SqlCodeDomBuilder();
        }

        [Fact]
        public void WhileDo()
        {
            Func<IDictionary<string, object>, object> func;
            object result;
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment();

            func = environment.Parse("test",
                "SET factorial=1, n=1" +
                "WHILE ?n <= 5 LOOP" +
                "   SET factorial = ?factorial * ?n" +
                "   SET n = ?n + 1" +
                "END LOOP " +
                "EXIT WITH ?factorial"
            );
            result = func(null);
            ((int)result).Should().Be(120);

            func = environment.Parse("test",
                "SET factorial=1, n=0" +
                "WHILE ?n <= 5 LOOP" +
                "   IF ?n = 0 THEN SET n = 1; CONTINUE; END IF " +
                "   IF ?n = 5 THEN BREAK; END IF " +
                "   SET factorial = ?factorial * ?n" +
                "   SET n = ?n + 1" +
                "END LOOP " +
                "EXIT WITH ?factorial"
            );
            result = func(null);
            ((int)result).Should().Be(24);

            func = environment.Parse("test",
                "SET factorial=1, n=1;" +
                "WHILE TRUE LOOP" +
                "   SET factorial = ?factorial * ?n" +
                "   SET n = ?n + 1" +
                "   IF ?n > 5 THEN BREAK; END IF " +
                "END LOOP " +
                "EXIT WITH ?factorial"
            );
            result = func(null);
            ((int)result).Should().Be(120);
        }

        [Fact]
        public void WhileDoParseError()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment();
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "CONTINUE"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "BREAK"
                )
            );
        }
    }
}
