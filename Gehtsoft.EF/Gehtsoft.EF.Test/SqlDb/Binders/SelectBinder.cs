using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
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

        private static DummyDbDataReaderResult CreateResult1()
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
                    { "e1", DbType.Int32 }
                },

                Data = new DummyDbDataReaderColumnDataRows()
                {
                    { (short)1, (int)int.MinValue, (long)long.MaxValue, (double)1.23, (decimal)4.56, new DateTime(2020, 5, 22), new DateTime(2021, 7, 23, 11, 54, 12), new TimeSpan(1, 22, 33), "1", "c25f12a3-36fb-4263-be31-773f675d9aa9", new byte[] { 1, 2, 3 }, "abcd", (int)TestEnum.V1 },
                    { DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value },
                }
            };
            return r;
        }

        private static DummyDbDataReader CreateReader1()
        {
            return new DummyDbDataReader()
            {
                Results = new DummyDbDataReaderResultCollection()
                {
                    CreateResult1()
                }
            };
        }

        public class Type1
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

        public class Type2
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

        [Entity]
        public class Entity1
        {
            [EntityProperty]
            public short? F1 { get; set; }
            [EntityProperty]
            public int? F2 { get; set; }
            [EntityProperty]
            public long? F3 { get; set; }
            [EntityProperty]
            public double? F4 { get; set; }
            [EntityProperty]
            public decimal? F5 { get; set; }
            [EntityProperty]
            public DateTime? F6 { get; set; }
            [EntityProperty]
            public DateTime? F7 { get; set; }
            [EntityProperty]
            public TimeSpan? F8 { get; set; }
            [EntityProperty]
            public bool? F9 { get; set; }
            [EntityProperty]
            public Guid? F10 { get; set; }
            [EntityProperty]
            public byte[] F11 { get; set; }
            [EntityProperty]
            public string F12 { get; set; }
        }

        public class Entity2 : DynamicEntity
        {
            public override EntityAttribute EntityAttribute => new EntityAttribute() { Table = "e1" };

            protected override IEnumerable<IDynamicEntityProperty> InitializeProperties()
            {
                var e = AllEntities.Get(typeof(Entity1));
                for (int i = 0; i < e.TableDescriptor.Count; i++)
                {
                    yield return new DynamicEntityProperty()
                    {
                        EntityPropertyAttribute = new EntityPropertyAttribute()
                        {
                            Field = e.TableDescriptor[i].Name,
                            DbType = e.TableDescriptor[i].DbType,
                            Size = e.TableDescriptor[i].Size,
                            Precision = e.TableDescriptor[i].Precision,
                        },
                        Name = e.TableDescriptor[i].ID,
                        PropertyType = e.TableDescriptor[i].PropertyAccessor.PropertyType
                    };
                }
            }
        }

        [Fact]
        public void AutoBindSimpleType()
        {
            SelectQueryResultBinder binder = new SelectQueryResultBinder(typeof(Type1));
            binder.AutoBindType();
            binder.Rules.Count.Should().BeGreaterOrEqualTo(12);
            for (int i = 1; i < 12; i++)
                binder.Rules.Should().Contain(r => r.ColumnName == "F" + i && r.PropertyAccessor.Name == "F" + i);
        }

        [Fact]
        public void AutoBindSimpleType_WithPrefix()
        {
            SelectQueryResultBinder binder = new SelectQueryResultBinder(typeof(Type1));
            binder.AutoBindType("prefix_");
            binder.Rules.Count.Should().BeGreaterOrEqualTo(12);
            for (int i = 1; i < 12; i++)
                binder.Rules.Should().Contain(r => r.ColumnName == "prefix_F" + i && r.PropertyAccessor.Name == "F" + i);
        }

        [Fact]
        public void AutoBindEntity()
        {
            SelectQueryResultBinder binder = new SelectQueryResultBinder(typeof(Entity1));
            binder.AutoBindType();
            binder.Rules.Should().HaveCount(12);
            for (int i = 1; i < 12; i++)
            {
                binder.Rules[i - 1].ColumnName.Should().Be("f" + i);
                binder.Rules[i - 1].PropertyAccessor.Should().NotBeNull();
                binder.Rules[i - 1].PropertyAccessor.Name.Should().Be("F" + i);
            }
        }

        [Fact]
        public void AutoBindDynamicEntity()
        {
            SelectQueryResultBinder binder = new SelectQueryResultBinder(typeof(Entity2));
            binder.AutoBindType();
            binder.Rules.Should().HaveCount(12);
            for (int i = 1; i < 12; i++)
            {
                binder.Rules[i - 1].ColumnName.Should().Be("f" + i);
                binder.Rules[i - 1].PropertyAccessor.Should().NotBeNull();
                binder.Rules[i - 1].PropertyAccessor.Name.Should().Be("F" + i);
            }

            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader1();
            query.ExecuteReader();
        }

        [Fact]
        public void ReadSimpleType_NotNullable()
        {
            SelectQueryResultBinder binder = new SelectQueryResultBinder(typeof(Type1));
            binder.AutoBindType();

            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader1();
            query.ExecuteReader();

            query.ReadNext();
            var e = binder.Read<Type1>(query);

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
            e.F11.Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
            e.F12.Should().Be("abcd");

            query.ReadNext();
            e = binder.Read<Type1>(query);

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
        public void ReadSimpleType_Nullable()
        {
            SelectQueryResultBinder binder = new SelectQueryResultBinder(typeof(Type2));
            binder.AutoBindType();

            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader1();
            query.ExecuteReader();

            query.ReadNext();
            var e = binder.Read<Type2>(query);

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
            e = binder.Read<Type2>(query);

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
        public void ReadSimpleType_ManualBind()
        {
            SelectQueryResultBinder binder = new SelectQueryResultBinder(typeof(Type2));
            binder.AddBinding("f1", nameof(Type2.F1));
            binder.AddBinding("f2", nameof(Type2.F2));
            binder.AddBinding("f3", nameof(Type2.F3));
            binder.AddBinding("f4", nameof(Type2.F4));
            binder.AddBinding("f5", nameof(Type2.F5));
            binder.AddBinding("f6", nameof(Type2.F6));
            binder.AddBinding("f7", nameof(Type2.F7));
            binder.AddBinding("f8", nameof(Type2.F8));
            binder.AddBinding("f9", nameof(Type2.F9));
            binder.AddBinding("f10", nameof(Type2.F10));
            binder.AddBinding("f11", nameof(Type2.F11));
            binder.AddBinding("f12", nameof(Type2.F12));

            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader1();
            query.ExecuteReader();

            query.ReadNext();
            var e = binder.Read<Type2>(query);

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
            e = binder.Read<Type2>(query);

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
        public void ReadEntity()
        {
            SelectQueryResultBinder binder = new SelectQueryResultBinder(typeof(Entity1));
            binder.AutoBindType();

            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader1();
            query.ExecuteReader();

            query.ReadNext();
            var e = binder.Read<Entity1>(query);

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
        }

        [Fact]
        public void ReadDynamicEntity_AsEntity()
        {
            SelectQueryResultBinder binder = new SelectQueryResultBinder(typeof(Entity2));
            binder.AutoBindType();

            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader1();
            query.ExecuteReader();

            query.ReadNext();
            dynamic e = binder.Read<Entity2>(query);

            ((object)e.F1).Should().Be(1);
            ((object)e.F2).Should().Be(int.MinValue);
            ((object)e.F3).Should().Be(long.MaxValue);
            ((object)e.F4).Should().Be(1.23);
            ((object)e.F5).Should().Be(4.56m);
            ((object)e.F6).Should().Be(new DateTime(2020, 5, 22));
            ((object)e.F7).Should().Be(new DateTime(2021, 7, 23, 11, 54, 12));
            ((object)e.F8).Should().Be(new TimeSpan(1, 22, 33));
            ((object)e.F9).Should().Be(true);
            ((object)e.F10).Should().Be(Guid.Parse("c25f12a3-36fb-4263-be31-773f675d9aa9"));
            ((object)e.F11).Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
            ((object)e.F12).Should().Be("abcd");
        }

        [Fact]
        public void ReadDynamicEntity_AsDynamic()
        {
            SelectQueryResultBinder binder = new SelectQueryResultBinder(typeof(Entity2));
            binder.AutoBindType();

            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader1();
            query.ExecuteReader();

            query.ReadNext();
            dynamic e = binder.ReadToDynamic(query);

            ((object)e.F1).Should().Be(1);
            ((object)e.F2).Should().Be(int.MinValue);
            ((object)e.F3).Should().Be(long.MaxValue);
            ((object)e.F4).Should().Be(1.23);
            ((object)e.F5).Should().Be(4.56m);
            ((object)e.F6).Should().Be(new DateTime(2020, 5, 22));
            ((object)e.F7).Should().Be(new DateTime(2021, 7, 23, 11, 54, 12));
            ((object)e.F8).Should().Be(new TimeSpan(1, 22, 33));
            ((object)e.F9).Should().Be(true);
            ((object)e.F10).Should().Be(Guid.Parse("c25f12a3-36fb-4263-be31-773f675d9aa9"));
            ((object)e.F11).Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
            ((object)e.F12).Should().Be("abcd");
        }

        [Fact]
        public void BindQueryAndReadDynamic()
        {
            SelectQueryResultBinder binder = new SelectQueryResultBinder(typeof(ExpandoObject));

            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader1();
            query.ExecuteReader();
            query.ReadNext();

            binder.AutoBindQuery(query);

            dynamic e = binder.ReadToDynamic(query);

            ((object)e.f1).Should().Be(1);
            ((object)e.f2).Should().Be(int.MinValue);
            ((object)e.f3).Should().Be(long.MaxValue);
            ((object)e.f4).Should().Be(1.23);
            ((object)e.f5).Should().Be(4.56m);
            ((object)e.f6).Should().Be(new DateTime(2020, 5, 22));
            ((object)e.f7).Should().Be(new DateTime(2021, 7, 23, 11, 54, 12));
            ((object)e.f8).Should().Be(new TimeSpan(1, 22, 33));
            ((object)e.f9).Should().Be("1");
            ((object)e.f10).Should().Be("c25f12a3-36fb-4263-be31-773f675d9aa9");
            ((object)e.f11).Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
            ((object)e.f12).Should().Be("abcd");

            query.ReadNext();

            e = binder.ReadToDynamic(query);
            ((object)e.f1).Should().Be(null);
        }

        [Fact]
        public void BindQueryWithPrefix()
        {
            SelectQueryResultBinder binder = new SelectQueryResultBinder(typeof(ExpandoObject));

            using var efconnection = new DummySqlConnection();
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader1();
            query.ExecuteReader();
            query.ReadNext();

            binder.AutoBindQuery(query, "f");

            binder.Rules.Should().HaveCount(13);
            for (int i = 1; i < 12; i++)
            {
                binder.Rules[i - 1].ColumnName.Should().Be("f" + i);
                binder.Rules[i - 1].PropertyAccessor.Should().NotBeNull();
                binder.Rules[i - 1].PropertyAccessor.Name.Should().Be(i.ToString());
            }
        }

        [Fact]
        public void AddBindingErrors()
        {
            SelectQueryResultBinder binder = new SelectQueryResultBinder(typeof(Entity1));

            ((Action)(() => new SelectQueryResultBinder(null))).Should().Throw<ArgumentException>();

            ((Action)(() => binder.AddBinding(-1, nameof(Entity1.F1)))).Should().Throw<ArgumentException>();
            ((Action)(() => binder.AddBinding(0, "F0"))).Should().Throw<ArgumentException>();

            ((Action)(() => binder.AddBinding((SelectQueryResultBinder)null, nameof(Entity1.F1)))).Should().Throw<ArgumentException>();
            ((Action)(() => binder.AddBinding(binder, "F0"))).Should().Throw<ArgumentException>();

            using var efconnection = new DummySqlConnection();
            using var query = efconnection.GetQuery("command");
            query.ExecuteNoData();

            ((Action)(() => binder.AutoBindQuery(null))).Should().Throw<ArgumentException>();
            ((Action)(() => binder.AutoBindQuery(query))).Should().Throw<ArgumentException>();
            ((Action)(() => binder.Read(null))).Should().Throw<ArgumentException>();
            ((Action)(() => binder.Read(query))).Should().Throw<ArgumentException>();
        }

        private static DummyDbDataReaderResult CreateResult2()
        {
            var r = new DummyDbDataReaderResult()
            {
                Columns = new DummyDbDataReaderColumnCollection()
                {
                    { "id", DbType.Int32 },
                    { "name", DbType.String },
                    { "ref_id", DbType.Int32 },
                    { "ref_name", DbType.Int32 },
                },

                Data = new DummyDbDataReaderColumnDataRows()
                {
                    { 1, "name1", 3, "ref_name" },
                    { 2, "name2", DBNull.Value, DBNull.Value },
                }
            };
            return r;
        }

        private static DummyDbDataReader CreateReader2()
        {
            return new DummyDbDataReader()
            {
                Results = new DummyDbDataReaderResultCollection()
                {
                    CreateResult2()
                }
            };
        }

        [Entity]
        public class TestDict
        {
            [PrimaryKey]
            public int ID { get; set; }
            [EntityProperty]
            public string Name { get; set; }
        }

        public class TestTable
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public TestDict Ref { get; set; }
        }

        [Fact]
        public void SubRule()
        {
            SelectQueryResultBinder binderDict = new SelectQueryResultBinder(typeof(TestDict));
            binderDict.AutoBindType("ref_");

            SelectQueryResultBinder binderTable = new SelectQueryResultBinder(typeof(TestTable));
            binderTable.AutoBindType("");
            binderTable.AddBinding(binderDict, nameof(TestTable.Ref));

            using var efconnection = new DummySqlConnection();
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader2();
            query.ExecuteReader();

            query.ReadNext();
            var t = binderTable.Read<TestTable>(query);

            t.ID.Should().Be(1);
            t.Name.Should().Be("name1");
            t.Ref.Should().NotBeNull();
            t.Ref.ID.Should().Be(3);
            t.Ref.Name.Should().Be("ref_name");

            query.ReadNext();
            t = binderTable.Read<TestTable>(query);

            t.ID.Should().Be(2);
            t.Name.Should().Be("name2");
            t.Ref.Should().BeNull();
        }
    }
}

