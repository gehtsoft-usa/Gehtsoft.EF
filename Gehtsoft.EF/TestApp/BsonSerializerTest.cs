using System;
using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Bson;
using Gehtsoft.EF.Entities;
using MongoDB.Bson;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace TestApp
{
    [TestFixture]
    public class BsonSerializerTest
    {
        public enum TestEnum
        {
            Value1,
            Value2,
            Value3,
            Value4,
            Value5,
        }

        [Entity(Table = "test1dicttable")]
        public sealed class EntityTestDict : IEquatable<EntityTestDict>
        {
            [EntityProperty(AutoId = true)]
            public ObjectId ID { get; set; }

            [EntityProperty(Sorted = true)]
            public string Name { get; set; }

            public bool Equals(EntityTestDict other)
            {
                if (other is null) return false;
                if (ReferenceEquals(this, other)) return true;
                return ID.Equals(other.ID) && string.Equals(Name, other.Name);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((EntityTestDict)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (ID.GetHashCode() * 397) ^ ((Name?.GetHashCode()) ?? 0);
                }
            }
        }

        [Entity(Table = "tes1talltable")]
        public class EntityTestAll
        {
            [EntityProperty]
            public string StringValue { get; set; }

            [EntityProperty]
            public int IntValue { get; set; }

            [EntityProperty]
            public long LongValue { get; set; }

            [EntityProperty]
            public double DoubleValue { get; set; }

            [EntityProperty]
            public decimal DecimalValue { get; set; }

            [EntityProperty]
            public DateTime DateTimeValue { get; set; }

            [EntityProperty]
            public bool BoolValue { get; set; }

            [EntityProperty]
            public byte[] BinaryValue { get; set; }

            [EntityProperty]
            public TestEnum EnumValue { get; set; }

            [EntityProperty]
            public Guid GuidValue { get; set; }

            [EntityProperty]
            public EntityTestDict DictValue { get; set; }

            [EntityProperty(ForeignKey = true)]
            public EntityTestDict DictValueRef { get; set; }
        }

        [Entity(Table = "tes1talltable1")]
        public class EntityTestAllNullable
        {
            [EntityProperty]
            public int? IntValue { get; set; }

            [EntityProperty]
            public long? LongValue { get; set; }

            [EntityProperty]
            public double? DoubleValue { get; set; }

            [EntityProperty]
            public decimal? DecimalValue { get; set; }

            [EntityProperty]
            public DateTime? DateTimeValue { get; set; }

            [EntityProperty]
            public bool? BoolValue { get; set; }

            [EntityProperty]
            public TestEnum? EnumValue { get; set; }

            [EntityProperty]
            public Guid? GuidValue { get; set; }
        }

        [Entity(Table = "tes1talltable2")]
        public class EntityTestAllArr
        {
            [EntityProperty]
            public string[] StringValue { get; set; }

            [EntityProperty]
            public int[] IntValue { get; set; }

            [EntityProperty]
            public long[] LongValue { get; set; }

            [EntityProperty]
            public double[] DoubleValue { get; set; }

            [EntityProperty]
            public decimal[] DecimalValue { get; set; }

            [EntityProperty]
            public DateTime?[] DateTimeValue { get; set; }

            [EntityProperty]
            public bool[] BoolValue { get; set; }

            [EntityProperty]
            public TestEnum[] EnumValue { get; set; }

            [EntityProperty]
            public Guid[] GuidValue { get; set; }

            [EntityProperty]
            public EntityTestDict[] DictValue { get; set; }
        }

        private readonly EntityTestDict d1 = new EntityTestDict() { ID = ObjectId.GenerateNewId(), Name = "Name 1" },
            d2 = new EntityTestDict() { ID = ObjectId.GenerateNewId(), Name = "Name 2" },
            d3 = new EntityTestDict() { ID = ObjectId.GenerateNewId(), Name = "Name 3" },
            d4 = new EntityTestDict() { ID = ObjectId.GenerateNewId(), Name = "Name 4" };

        [Test]
        public void TestBsonTypes()
        {
            byte[] binary = new byte[] { 1, 2, 7, 9 };
            DateTime dt = DateTime.Now;
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, DateTimeKind.Unspecified);

            EntityTestAll test1 = new EntityTestAll
            {
                StringValue = "S1",
                BoolValue = true,
                IntValue = 123,
                LongValue = 345,
                DoubleValue = 789.12,
                DecimalValue = 678.91m,
                GuidValue = Guid.NewGuid(),
                DateTimeValue = DateTime.Now
            }, test2;
            test1.DateTimeValue = dt;
            test1.BinaryValue = binary;
            test1.DictValue = d1;
            test1.DictValueRef = d2;

            BsonDocument doc = test1.ConvertToBson();
            test2 = doc.ToEntity(typeof(EntityTestAll)) as EntityTestAll;

            ClassicAssert.AreEqual(test1.StringValue, test2.StringValue);
            ClassicAssert.AreEqual(test1.BoolValue, test2.BoolValue);
            ClassicAssert.AreEqual(test1.IntValue, test2.IntValue);
            ClassicAssert.AreEqual(test1.LongValue, test2.LongValue);
            ClassicAssert.AreEqual(test1.DoubleValue, test2.DoubleValue);
            ClassicAssert.AreEqual(test1.DecimalValue, test2.DecimalValue);
            ClassicAssert.AreEqual(test1.GuidValue, test2.GuidValue);
            ClassicAssert.AreEqual(test1.DateTimeValue, test2.DateTimeValue);
            ClassicAssert.AreEqual(test1.BinaryValue, test2.BinaryValue);
            ClassicAssert.AreEqual(test1.DictValue?.ID, test2.DictValue?.ID);
            ClassicAssert.AreEqual(test1.DictValue?.Name, test2.DictValue?.Name);
            ClassicAssert.AreEqual(test1.DictValueRef?.ID, test2.DictValueRef?.ID);
            ClassicAssert.AreEqual(null, test2.DictValueRef?.Name);

            test1.StringValue = null;
            test1.BinaryValue = null;
            test1.DictValue = null;
            test1.DictValueRef = null;
            test1.BoolValue = false;
            test1.GuidValue = Guid.NewGuid();
            test1.EnumValue = TestEnum.Value5;

            doc = test1.ConvertToBson();
            test2 = doc.ToEntity(typeof(EntityTestAll)) as EntityTestAll;
            ClassicAssert.IsNull(test2.StringValue);
            ClassicAssert.IsNull(test2.BinaryValue);
            ClassicAssert.IsNull(test2.DictValue);
            ClassicAssert.IsNull(test2.DictValueRef);
            ClassicAssert.IsFalse(test2.BoolValue);
            ClassicAssert.AreEqual(TestEnum.Value5, test2.EnumValue);
        }

        [Test]
        public void TestBsonNullable()
        {
            DateTime dt = DateTime.Now;
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, DateTimeKind.Unspecified);

            EntityTestAllNullable test1 = new EntityTestAllNullable
            {
                BoolValue = true,
                IntValue = 123,
                LongValue = 345,
                DoubleValue = 789.12,
                DecimalValue = 678.91m,
                GuidValue = Guid.NewGuid(),
                DateTimeValue = dt,
                EnumValue = TestEnum.Value3
            }, test2;

            BsonDocument doc = test1.ConvertToBson();
            test2 = doc.ToEntity(typeof(EntityTestAllNullable)) as EntityTestAllNullable;

            ClassicAssert.AreEqual(test1.BoolValue, test2.BoolValue);
            ClassicAssert.AreEqual(test1.IntValue, test2.IntValue);
            ClassicAssert.AreEqual(test1.LongValue, test2.LongValue);
            ClassicAssert.AreEqual(test1.DoubleValue, test2.DoubleValue);
            ClassicAssert.AreEqual(test1.DecimalValue, test2.DecimalValue);
            ClassicAssert.AreEqual(test1.GuidValue, test2.GuidValue);
            ClassicAssert.AreEqual(test1.DateTimeValue, test2.DateTimeValue);

            test1.BoolValue = null;
            test1.IntValue = null;
            test1.LongValue = null;
            test1.DoubleValue = null;
            test1.DecimalValue = null;
            test1.GuidValue = null;
            test1.DateTimeValue = null;
            test1.DateTimeValue = null;
            test1.EnumValue = null;

            doc = test1.ConvertToBson();
            test2 = doc.ToEntity(typeof(EntityTestAllNullable)) as EntityTestAllNullable;

            ClassicAssert.AreEqual(test1.BoolValue, test2.BoolValue);
            ClassicAssert.AreEqual(test1.IntValue, test2.IntValue);
            ClassicAssert.AreEqual(test1.LongValue, test2.LongValue);
            ClassicAssert.AreEqual(test1.DoubleValue, test2.DoubleValue);
            ClassicAssert.AreEqual(test1.DecimalValue, test2.DecimalValue);
            ClassicAssert.AreEqual(test1.GuidValue, test2.GuidValue);
            ClassicAssert.AreEqual(test1.DateTimeValue, test2.DateTimeValue);
        }

        [Test]
        public void TestBsonArray()
        {
            DateTime dt = DateTime.Now;
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, DateTimeKind.Unspecified);

            EntityTestAllArr test1 = new EntityTestAllArr(), test2;
            BsonDocument doc;

            test1.StringValue = null;
            test1.BoolValue = null;
            test1.IntValue = null;
            test1.LongValue = null;
            test1.DoubleValue = null;
            test1.DecimalValue = null;
            test1.GuidValue = null;
            test1.DateTimeValue = null;
            test1.DictValue = null;
            test1.EnumValue = null;

            doc = test1.ConvertToBson();
            test2 = doc.ToEntity(typeof(EntityTestAllArr)) as EntityTestAllArr;

            ClassicAssert.AreEqual(test1.StringValue, test2.StringValue);
            ClassicAssert.AreEqual(test1.BoolValue, test2.BoolValue);
            ClassicAssert.AreEqual(test1.IntValue, test2.IntValue);
            ClassicAssert.AreEqual(test1.LongValue, test2.LongValue);
            ClassicAssert.AreEqual(test1.DoubleValue, test2.DoubleValue);
            ClassicAssert.AreEqual(test1.DecimalValue, test2.DecimalValue);
            ClassicAssert.AreEqual(test1.GuidValue, test2.GuidValue);
            ClassicAssert.AreEqual(test1.DateTimeValue, test2.DateTimeValue);
            ClassicAssert.AreEqual(test1.EnumValue, test2.EnumValue);
            ClassicAssert.AreEqual(test1.DictValue, test2.DictValue);

            test1.StringValue = new string[] { };
            test1.BoolValue = new bool[] { };
            test1.IntValue = new int[] { };
            test1.LongValue = new long[] { };
            test1.DoubleValue = new double[] { };
            test1.DecimalValue = new decimal[] { };
            test1.GuidValue = new Guid[] { };
            test1.DateTimeValue = new DateTime?[] { };
            test1.DictValue = new EntityTestDict[] { };
            test1.EnumValue = new TestEnum[] { };

            doc = test1.ConvertToBson();
            test2 = doc.ToEntity(typeof(EntityTestAllArr)) as EntityTestAllArr;

            ClassicAssert.AreEqual(test1.StringValue, test2.StringValue);
            ClassicAssert.AreEqual(test1.BoolValue, test2.BoolValue);
            ClassicAssert.AreEqual(test1.IntValue, test2.IntValue);
            ClassicAssert.AreEqual(test1.LongValue, test2.LongValue);
            ClassicAssert.AreEqual(test1.DoubleValue, test2.DoubleValue);
            ClassicAssert.AreEqual(test1.DecimalValue, test2.DecimalValue);
            ClassicAssert.AreEqual(test1.GuidValue, test2.GuidValue);
            ClassicAssert.AreEqual(test1.DateTimeValue, test2.DateTimeValue);
            ClassicAssert.AreEqual(test1.EnumValue, test2.EnumValue);
            ClassicAssert.AreEqual(test1.DictValue, test2.DictValue);

            test1.StringValue = new string[] { "aaa", null, "bbbb" };
            test1.BoolValue = new bool[] { true, false, true };
            test1.IntValue = new int[] { 1, 2, 3 };
            test1.LongValue = new long[] { 4, 5 };
            test1.DoubleValue = new double[] { 1, 2, 3, 4.5, 5.5, 6.7, 9.9 };
            test1.DecimalValue = new decimal[] { 123.45m };
            test1.GuidValue = new Guid[] { Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid() };
            test1.DateTimeValue = new DateTime?[] { dt.AddMinutes(246), dt.AddDays(-500), dt.AddMilliseconds(12345679), null, dt };
            test1.DictValue = new EntityTestDict[] { d3, d4, d1, null, d2 };
            test1.EnumValue = new TestEnum[] { TestEnum.Value3, TestEnum.Value4, TestEnum.Value1 };

            doc = test1.ConvertToBson();
            test2 = doc.ToEntity(typeof(EntityTestAllArr)) as EntityTestAllArr;

            ClassicAssert.AreEqual(test1.StringValue, test2.StringValue);
            ClassicAssert.AreEqual(test1.BoolValue, test2.BoolValue);
            ClassicAssert.AreEqual(test1.IntValue, test2.IntValue);
            ClassicAssert.AreEqual(test1.LongValue, test2.LongValue);
            ClassicAssert.AreEqual(test1.DoubleValue, test2.DoubleValue);
            ClassicAssert.AreEqual(test1.DecimalValue, test2.DecimalValue);
            ClassicAssert.AreEqual(test1.GuidValue, test2.GuidValue);
            ClassicAssert.AreEqual(test1.DateTimeValue, test2.DateTimeValue);
            ClassicAssert.AreEqual(test1.EnumValue, test2.EnumValue);
            ClassicAssert.AreEqual(test1.DictValue, test2.DictValue);
        }
    }
}