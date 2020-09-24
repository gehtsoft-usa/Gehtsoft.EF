﻿using FluentAssertions;
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
    public class WhileDoRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private ISqlDbConnectionFactory connectionFactory;
        private SqlDbConnection connection;

        public WhileDoRun()
        {
            connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.SQLITE, @"Data Source=:memory:"); ;
            Snapshot snapshot = new Snapshot();
            connection = connectionFactory.GetConnection();
            snapshot.CreateAsync(connection).ConfigureAwait(true).GetAwaiter().GetResult();
            EntityFinder.EntityTypeInfo[] entities = EntityFinder.FindEntities(new Assembly[] { typeof(Snapshot).Assembly }, "northwind", false);
            DomBuilder = new SqlCodeDomBuilder();
            DomBuilder.Build(entities, "entities");
        }

        public void Dispose()
        {
            if (connectionFactory.NeedDispose)
                connection.Dispose();
        }

        [Fact]
        public void WhileDoWithRun()
        {
            object result;
            SqlCodeDomBuilder environment = DomBuilder.NewEnvironment();

            environment.Parse("test",
                "SET factorial=1, n=1" +
                "WHILE ?n <= 5 LOOP" +
                "   SET factorial = ?factorial * ?n" +
                "   SET n = ?n + 1" +
                "END LOOP " +
                "EXIT WITH ?factorial"
            );
            result = environment.Run(connection);
            ((int)result).Should().Be(120);

            environment.Parse("test",
                "SET factorial=1, n=0" +
                "WHILE ?n <= 5 LOOP" +
                "   IF ?n = 0 THEN SET n = 1; CONTINUE; END IF " +
                "   IF ?n = 5 THEN BREAK; END IF " +
                "   SET factorial = ?factorial * ?n" +
                "   SET n = ?n + 1" +
                "END LOOP " +
                "EXIT WITH ?factorial"
            );
            result = environment.Run(connection);
            ((int)result).Should().Be(24);

            environment.Parse("test",
                "SET factorial=1, n=1" +
                "WHILE TRUE LOOP" +
                "   SET factorial = ?factorial * ?n" +
                "   SET n = ?n + 1" +
                "   IF ?n > 5 THEN BREAK; END IF " +
                "END LOOP " +
                "EXIT WITH ?factorial"
            );
            result = environment.Run(connection);
            ((int)result).Should().Be(120);
        }

        [Fact]
        public void WhileDoWithLinq()
        {
            Expression block;
            object result;
            SqlCodeDomBuilder environment = DomBuilder.NewEnvironment(connection);

            block = environment.ParseToLinq("test",
                "SET factorial=1, n=1" +
                "WHILE ?n <= 5 LOOP" +
                "   SET factorial = ?factorial * ?n" +
                "   SET n = ?n + 1" +
                "END LOOP " +
                "EXIT WITH ?factorial"
            );
            result = Expression.Lambda<Func<object>>(block).Compile()();
            ((int)result).Should().Be(120);

            block = environment.ParseToLinq("test",
                "SET factorial=1, n=0" +
                "WHILE ?n <= 5 LOOP" +
                "   IF ?n = 0 THEN SET n = 1; CONTINUE; END IF " +
                "   IF ?n = 5 THEN BREAK; END IF " +
                "   SET factorial = ?factorial * ?n" +
                "   SET n = ?n + 1" +
                "END LOOP " +
                "EXIT WITH ?factorial"
            );
            result = Expression.Lambda<Func<object>>(block).Compile()();
            ((int)result).Should().Be(24);

            block = environment.ParseToLinq("test",
                "SET factorial=1, n=1" +
                "WHILE TRUE LOOP" +
                "   SET factorial = ?factorial * ?n" +
                "   SET n = ?n + 1" +
                "   IF ?n > 5 THEN BREAK; END IF " +
                "END LOOP " +
                "EXIT WITH ?factorial"
            );
            result = Expression.Lambda<Func<object>>(block).Compile()();
            ((int)result).Should().Be(120);
        }
    }
}