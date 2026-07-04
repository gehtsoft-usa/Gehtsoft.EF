using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.DataManagement
{
    public class DynamicPropertiesInsertTest
    {
        private static readonly DateTime SampleUtc = new DateTime(2021, 12, 27, 12, 55, 17, DateTimeKind.Utc);

        [Entity(Scope = "dp_insert", Table = "dp_insert_owner")]
        [DynamicProperties]
        public class Owner : IDynamicPropertiesOwner
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }

            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        [Entity(Scope = "dp_insert", Table = "dp_insert_plain")]
        public class PlainOwner
        {
            [AutoId]
            public int Id { get; set; }
        }

        private sealed class Row
        {
            public int Type;
            public object Str;
            public object Int;
            public object Real;
        }

        private static Dictionary<string, Row> ReadProps(SqlDbConnection connection, string table, int owner)
        {
            var rows = new Dictionary<string, Row>();
            using (var query = connection.GetQuery($"SELECT name, prop_type, v_str, v_int, v_real FROM {table} WHERE owner = {owner}", true))
            {
                query.ExecuteReader();
                while (query.ReadNext())
                {
                    rows[query.GetValue<string>(0)] = new Row
                    {
                        Type = query.GetValue<int>(1),
                        Str = query.IsNull(2) ? null : query.GetValue<string>(2),
                        Int = query.IsNull(3) ? null : (object)query.GetValue<long>(3),
                        Real = query.IsNull(4) ? null : (object)query.GetValue<double>(4),
                    };
                }
            }
            return rows;
        }

        [Fact]
        public void Insert_PersistsAllTypes_InCorrectColumns()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();
            using (var q = connection.GetCreateEntityQuery<Owner>())
                q.Execute();

            var e = new Owner { Name = "e1" };
            DynamicPropertyBag bag = e.InitializeDynamicProperties();
            bag.Set("s", "hello");
            bag.Set("i", 42);
            bag.Set("l", 9000000000L);
            bag.Set("r", 4.5);
            bag.Set("b", true);
            bag.Set("dt", SampleUtc);

            using (var q = connection.GetInsertEntityQuery<Owner>())
                q.Execute(e);

            e.Id.Should().BeGreaterThan(0);
            e.DynamicProperties.IsNew.Should().BeFalse("the bag becomes persisted after insert");
            e.DynamicProperties.AnyModified.Should().BeFalse();

            Dictionary<string, Row> rows = ReadProps(connection, "dp_insert_owner_props", e.Id);
            rows.Should().HaveCount(6);

            rows["s"].Type.Should().Be((int)DynamicPropertyValueType.String);
            rows["s"].Str.Should().Be("hello");
            rows["s"].Int.Should().BeNull();
            rows["s"].Real.Should().BeNull();

            rows["i"].Type.Should().Be((int)DynamicPropertyValueType.Integer);
            rows["i"].Int.Should().Be(42L);
            rows["i"].Str.Should().BeNull();
            rows["i"].Real.Should().BeNull();

            rows["l"].Type.Should().Be((int)DynamicPropertyValueType.Long);
            rows["l"].Int.Should().Be(9000000000L);

            rows["r"].Type.Should().Be((int)DynamicPropertyValueType.Real);
            rows["r"].Real.Should().Be(4.5);
            rows["r"].Int.Should().BeNull();

            rows["b"].Type.Should().Be((int)DynamicPropertyValueType.Boolean);
            rows["b"].Int.Should().Be(1L);

            rows["dt"].Type.Should().Be((int)DynamicPropertyValueType.DateTime);
            rows["dt"].Int.Should().Be(SampleUtc.Ticks);
        }

        [Fact]
        public void Insert_EmptyBag_WritesNoRows_ButClearsNew()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();
            using (var q = connection.GetCreateEntityQuery<Owner>())
                q.Execute();

            var e = new Owner { Name = "e1" };
            e.InitializeDynamicProperties();  // empty new bag

            using (var q = connection.GetInsertEntityQuery<Owner>())
                q.Execute(e);

            ReadProps(connection, "dp_insert_owner_props", e.Id).Should().BeEmpty();
            e.DynamicProperties.IsNew.Should().BeFalse();
        }

        [Fact]
        public void Insert_NoBag_WritesNoRows()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();
            using (var q = connection.GetCreateEntityQuery<Owner>())
                q.Execute();

            var e = new Owner { Name = "e1" };  // never initialized -> DynamicProperties == null

            using (var q = connection.GetInsertEntityQuery<Owner>())
                q.Execute(e);

            e.Id.Should().BeGreaterThan(0);
            ReadProps(connection, "dp_insert_owner_props", e.Id).Should().BeEmpty();
        }

        [Fact]
        public void Insert_NonNewBag_Throws()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();
            using (var q = connection.GetCreateEntityQuery<Owner>())
                q.Execute();

            var e = new Owner { Name = "e1" };
            DynamicPropertyBag bag = e.InitializeDynamicProperties();
            bag.Set("s", "x");
            bag.AcceptChanges();  // no longer a new bag

            using (var q = connection.GetInsertEntityQuery<Owner>())
            {
                e.Invoking(_ => q.Execute(e))
                 .Should().Throw<EfSqlException>()
                 .Which.ErrorCode.Should().Be(EfExceptionCode.DynamicPropertiesBagIsNotNew);
            }
        }

        [Fact]
        public void Insert_PlainEntity_Unaffected()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();
            using (var q = connection.GetCreateEntityQuery<PlainOwner>())
                q.Execute();

            var e = new PlainOwner();
            using (var q = connection.GetInsertEntityQuery<PlainOwner>())
                q.Execute(e);

            e.Id.Should().BeGreaterThan(0);
        }
    }
}
