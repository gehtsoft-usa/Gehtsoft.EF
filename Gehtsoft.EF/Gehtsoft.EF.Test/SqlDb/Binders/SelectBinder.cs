using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb.Binders
{
    public class SelectBinder
    {
        public enum TestEnum
        {
            V0 = 0,
            V1 = 1,
            V2 = 2,
            V3 = 3
        }

        private static DummyDbDataReaderResult CreateResult()
        {
            var r = new DummyDbDataReaderResult()
            {
                Columns = new DummyDbDataReaderColumnCollection()
                {
                    { "f1", DbType.Int16 },
                    { "f2", DbType.Int32 },
                    { "f3", DbType.Int64 },
                    { "f4", DbType.Double },
                    { "f5", DbType.Decimal },
                    { "f6", DbType.Date },
                    { "f7", DbType.DateTime },
                    { "f8", DbType.Time },
                    { "f9", DbType.Boolean },
                    { "f10", DbType.Guid },
                    { "f11", DbType.Binary },
                    { "f12", DbType.String },
                },

                Data = new DummyDbDataReaderColumnDataRows()
                {
                    { (short)1, (int)int.MinValue, (long)long.MaxValue, (double)1.23, (decimal)4.56, new DateTime(2020, 5, 22), new DateTime(2021, 7, 23, 11, 54, 12), new TimeSpan(1, 22, 33), "1", "c25f12a3-36fb-4263-be31-773f675d9aa9", new byte[] { 1, 2, 3 }, "abcd" },
                    { DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value },
                }
            };
            return r;
        }

        private static DummyDbDataReader CreateReader()
        {
            return new DummyDbDataReader()
            {
                Results = new DummyDbDataReaderResultCollection()
                {
                    CreateResult()
                }
            };
        }

        public class Entity1
        {
            public short F1 { get; set; }
            public TestEnum E1 { get; set; }
            public int F2 { get; set; }
            public long F3 { get; set; }
            public double F4 { get; set; }
            public decimal F5 { get; set; }
            public DateTime F6 { get; set; }
            public DateTime F7 { get; set; }
            public TimeSpan F8 { get; set; }
            public bool F9 { get; set; }
            public Guid F10 { get; set; }
            public byte[] F11 { get; set; }
            public string F12 { get; set; }
        }

        [Fact]
        public void ManualBind()
        {
            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader();
            query.ExecuteReader();

            var binder = new SelectQueryTypeBinder(typeof(Entity1));
            binder.AddBinding("f1", nameof(Entity1.F1));
            binder.AddBinding("f1", nameof(Entity1.E1));
            binder.AddBinding("f2", nameof(Entity1.F2));
            binder.AddBinding("f3", nameof(Entity1.F3));
            binder.AddBinding("f4", nameof(Entity1.F4));
            binder.AddBinding("f5", nameof(Entity1.F5));
            binder.AddBinding("f6", nameof(Entity1.F6));
            binder.AddBinding("f7", nameof(Entity1.F7));
            binder.AddBinding("f8", nameof(Entity1.F8));
            binder.AddBinding("f9", nameof(Entity1.F9));
            binder.AddBinding("f10", nameof(Entity1.F10));
            binder.AddBinding("f11", nameof(Entity1.F11));
            binder.AddBinding("f12", nameof(Entity1.F12));

            query.ReadNext();
            var e = binder.Read<Entity1>(query);

            e.F1.Should().Be(1);
            e.E1.Should().Be(TestEnum.V1);
            e.F2.Should().Be(int.MinValue);
            e.F3.Should().Be(long.MaxValue);
            e.F4.Should().Be(1.23);
            e.F5.Should().Be(4.56m);
            e.F6.Should().Be(new DateTime(2020, 5, 22));
            e.F7.Should().Be(new DateTime(2021, 7, 23, 11, 54, 12));
            e.F8.Should().Be(new TimeSpan(1, 22, 33));
            e.F9.Should().Be(true);
            e.F10.Should().Be(Guid.Parse("c25f12a3-36fb-4263-be31-773f675d9aa9"));
            e.F11.Should().BeEquivalentTo(new byte[] { 1, 2, 3});
            e.F12.Should().Be("abcd");

            query.ReadNext();
            e = binder.Read<Entity1>(query);

            e.F1.Should().Be(0);
            e.E1.Should().Be(TestEnum.V0);
            e.F2.Should().Be(0);
            e.F3.Should().Be(0);
            e.F4.Should().Be(0.0);
            e.F5.Should().Be(0m);
            e.F6.Should().Be(new DateTime(0));
            e.F7.Should().Be(new DateTime(0));
            e.F8.Should().Be(new TimeSpan(0));
            e.F9.Should().Be(false);
            e.F10.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000000"));
            e.F11.Should().BeNull();
            e.F12.Should().Be(null);
        }

        [Fact]
        public void AutoBind()
        {
            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader();
            query.ExecuteReader();

            var binder = new SelectQueryTypeBinder(typeof(Entity1));
            binder.AutoBind();

            query.ReadNext();
            var e = binder.Read<Entity1>(query);

            e.F1.Should().Be(1);
            e.E1.Should().Be(TestEnum.V0);
            e.F2.Should().Be(int.MinValue);
            e.F3.Should().Be(long.MaxValue);
            e.F4.Should().Be(1.23);
            e.F5.Should().Be(4.56m);
            e.F6.Should().Be(new DateTime(2020, 5, 22));
            e.F7.Should().Be(new DateTime(2021, 7, 23, 11, 54, 12));
            e.F8.Should().Be(new TimeSpan(1, 22, 33));
            e.F9.Should().Be(true);
            e.F10.Should().Be(Guid.Parse("c25f12a3-36fb-4263-be31-773f675d9aa9"));
            e.F11.Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
            e.F12.Should().Be("abcd");

            query.ReadNext();
            e = binder.Read<Entity1>(query);

            e.F1.Should().Be(0);
            e.E1.Should().Be(TestEnum.V0);
            e.F2.Should().Be(0);
            e.F3.Should().Be(0);
            e.F4.Should().Be(0.0);
            e.F5.Should().Be(0m);
            e.F6.Should().Be(new DateTime(0));
            e.F7.Should().Be(new DateTime(0));
            e.F8.Should().Be(new TimeSpan(0));
            e.F9.Should().Be(false);
            e.F10.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000000"));
            e.F11.Should().BeNull();
            e.F12.Should().Be(null);
        }

        public class Entity2
        {
            public short? F1 { get; set; }
            public int? F2 { get; set; }
            public long? F3 { get; set; }
            public double? F4 { get; set; }
            public decimal? F5 { get; set; }
            public DateTime? F6 { get; set; }
            public DateTime? F7 { get; set; }
            public TimeSpan? F8 { get; set; }
            public bool? F9 { get; set; }
            public Guid? F10 { get; set; }
            public byte[] F11 { get; set; }
            public string F12 { get; set; }
        }

        [Fact]
        public void AutoBindToNullable()
        {
            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader();
            query.ExecuteReader();

            var binder = new SelectQueryTypeBinder(typeof(Entity2));
            binder.AutoBind();

            query.ReadNext();
            var e = binder.Read<Entity2>(query);

            e.F1.Should().Be(1);
            e.F2.Should().Be(int.MinValue);
            e.F3.Should().Be(long.MaxValue);
            e.F4.Should().Be(1.23);
            e.F5.Should().Be(4.56m);
            e.F6.Should().Be(new DateTime(2020, 5, 22));
            e.F7.Should().Be(new DateTime(2021, 7, 23, 11, 54, 12));
            e.F8.Should().Be(new TimeSpan(1, 22, 33));
            e.F9.Should().Be(true);
            e.F10.Should().Be(Guid.Parse("c25f12a3-36fb-4263-be31-773f675d9aa9"));
            e.F11.Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
            e.F12.Should().Be("abcd");

            query.ReadNext();
            e = binder.Read<Entity2>(query);

            e.F1.Should().Be(null);
            e.F2.Should().Be(null);
            e.F3.Should().Be(null);
            e.F4.Should().Be(null);
            e.F5.Should().Be(null);
            e.F6.Should().Be(null);
            e.F7.Should().Be(null);
            e.F8.Should().Be(null);
            e.F9.Should().Be(null);
            e.F10.Should().Be(null);
            e.F11.Should().BeNull();
            e.F12.Should().Be(null);
        }

        [Fact]
        public void BindToDynamic()
        {
            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader();
            query.ExecuteReader();

            var binder = new SelectQueryTypeBinder(typeof(Entity2));

            dynamic e = new ExpandoObject();
            query.ReadNext();
            binder.BindToDynamic(query, e);

            ((object)(e.F1)).Should().Be(1);
            ((object)(e.F2)).Should().Be(int.MinValue);
            ((object)(e.F3)).Should().Be(long.MaxValue);
            ((object)(e.F4)).Should().Be(1.23);
            ((object)(e.F5)).Should().Be(4.56m);
            ((object)(e.F6)).Should().Be(new DateTime(2020, 5, 22));
            ((object)(e.F7)).Should().Be(new DateTime(2021, 7, 23, 11, 54, 12));
            ((object)(e.F8)).Should().Be(new TimeSpan(1, 22, 33));
            ((object)(e.F9)).Should().Be(true);
            ((object)(e.F10)).Should().Be(Guid.Parse("c25f12a3-36fb-4263-be31-773f675d9aa9"));
            ((object)(e.F11)).Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
            ((object)(e.F12)).Should().Be("abcd");

            query.ReadNext();
            e = new ExpandoObject();
            binder.BindToDynamic(query, e);

            ((object)(e.F1)).Should().Be(null);
            ((object)(e.F2)).Should().Be(null);
            ((object)(e.F3)).Should().Be(null);
            ((object)(e.F4)).Should().Be(null);
            ((object)(e.F5)).Should().Be(null);
            ((object)(e.F6)).Should().Be(null);
            ((object)(e.F7)).Should().Be(null);
            ((object)(e.F8)).Should().Be(null);
            ((object)(e.F9)).Should().Be(null);
            ((object)(e.F10)).Should().Be(null);
            ((object)(e.F11)).Should().Be(null);
            ((object)(e.F12)).Should().Be(null);
        }

        public class Entity3
        {
            public short F1 { get; set; }
            public TestEnum E1 { get; set; }
            public int F2 { get; set; }
            public long F3 { get; set; }
            public double F4 { get; set; }
            public decimal F5 { get; set; }
            public Entity4 Aggregate { get; set; }
        }

        public class Entity4
        {
            public DateTime F6 { get; set; }
            public DateTime F7 { get; set; }
            public TimeSpan F8 { get; set; }
            public bool F9 { get; set; }
            public Guid F10 { get; set; }
            public byte[] F11 { get; set; }
            public string F12 { get; set; }
        }

        [Fact]
        public void MultiBind()
        {
            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader();
            query.ExecuteReader();

            var aggregateBinder = new SelectQueryTypeBinder(typeof(Entity4));
            aggregateBinder.AddBinding("f6", nameof(Entity4.F6));
            aggregateBinder.AddBinding("f7", nameof(Entity4.F7));
            aggregateBinder.AddBinding("f8", nameof(Entity4.F8));
            aggregateBinder.AddBinding("f9", nameof(Entity4.F9));
            aggregateBinder.AddBinding("f10", nameof(Entity4.F10));
            aggregateBinder.AddBinding("f11", nameof(Entity4.F11));
            aggregateBinder.AddBinding("f12", nameof(Entity4.F12));

            var binder = new SelectQueryTypeBinder(typeof(Entity3));
            binder.AddBinding("f1", nameof(Entity3.F1));
            binder.AddBinding("f1", nameof(Entity3.E1));
            binder.AddBinding("f2", nameof(Entity3.F2));
            binder.AddBinding("f3", nameof(Entity3.F3));
            binder.AddBinding("f4", nameof(Entity3.F4));
            binder.AddBinding("f5", nameof(Entity3.F5));
            binder.AddBinding(aggregateBinder, nameof(Entity3.Aggregate));

            query.ReadNext();
            var e = binder.Read<Entity3>(query);

            e.F1.Should().Be(1);
            e.E1.Should().Be(TestEnum.V1);
            e.F2.Should().Be(int.MinValue);
            e.F3.Should().Be(long.MaxValue);
            e.F4.Should().Be(1.23);
            e.F5.Should().Be(4.56m);
            e.Aggregate.F6.Should().Be(new DateTime(2020, 5, 22));
            e.Aggregate.F7.Should().Be(new DateTime(2021, 7, 23, 11, 54, 12));
            e.Aggregate.F8.Should().Be(new TimeSpan(1, 22, 33));
            e.Aggregate.F9.Should().Be(true);
            e.Aggregate.F10.Should().Be(Guid.Parse("c25f12a3-36fb-4263-be31-773f675d9aa9"));
            e.Aggregate.F11.Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
            e.Aggregate.F12.Should().Be("abcd");
        }
    }
}

