﻿using System;
using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Bson;
using Gehtsoft.EF.Entities;
using MongoDB.Bson;
using NUnit.Framework;

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
        public class EntityTestDict : IEquatable<EntityTestDict>
        {
            [EntityProperty(AutoId = true)]
            public ObjectId ID { get; set; }

            [EntityProperty(Sorted = true)]
            public string Name { get; set; }

            public bool Equals(EntityTestDict other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return ID.Equals(other.ID) && string.Equals(Name, other.Name);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((EntityTestDict)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (ID.GetHashCode() * 397) ^ (Name != null ? Name.GetHashCode() : 0);
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

        private EntityTestDict d1 = new EntityTestDict() { ID = ObjectId.GenerateNewId(), Name = "Name 1" },
            d2 = new EntityTestDict() { ID = ObjectId.GenerateNewId(), Name = "Name 2" },
            d3 = new EntityTestDict() { ID = ObjectId.GenerateNewId(), Name = "Name 3" },
            d4 = new EntityTestDict() { ID = ObjectId.GenerateNewId(), Name = "Name 4" };

        [Test]
        public void TestBsonTypes()
        {
            byte[] binary = new byte[] { 1, 2, 7, 9 };
            DateTime dt = DateTime.Now;
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond);

            EntityTestAll test1 = new EntityTestAll(), test2;
            test1.StringValue = "S1";
            test1.BoolValue = true;
            test1.IntValue = 123;
            test1.LongValue = 345;
            test1.DoubleValue = 789.12;
            test1.DecimalValue = 678.91m;
            test1.GuidValue = Guid.NewGuid();
            test1.DateTimeValue = DateTime.Now;
            test1.DateTimeValue = dt;
            test1.BinaryValue = binary;
            test1.DictValue = d1;
            test1.DictValueRef = d2;

            BsonDocument doc = test1.ConvertToBson();
            test2 = EntityToBsonController.ToEntity(doc, typeof(EntityTestAll)) as EntityTestAll;

            Assert.AreEqual(test1.StringValue, test2.StringValue);
            Assert.AreEqual(test1.BoolValue, test2.BoolValue);
            Assert.AreEqual(test1.IntValue, test2.IntValue);
            Assert.AreEqual(test1.LongValue, test2.LongValue);
            Assert.AreEqual(test1.DoubleValue, test2.DoubleValue);
            Assert.AreEqual(test1.DecimalValue, test2.DecimalValue);
            Assert.AreEqual(test1.GuidValue, test2.GuidValue);
            Assert.AreEqual(test1.DateTimeValue, test2.DateTimeValue);
            Assert.AreEqual(test1.BinaryValue, test2.BinaryValue);
            Assert.AreEqual(test1.DictValue?.ID, test2.DictValue?.ID);
            Assert.AreEqual(test1.DictValue?.Name, test2.DictValue?.Name);
            Assert.AreEqual(test1.DictValueRef?.ID, test2.DictValueRef?.ID);
            Assert.AreEqual(null, test2.DictValueRef?.Name);

            test1.StringValue = null;
            test1.BinaryValue = null;
            test1.DictValue = null;
            test1.DictValueRef = null;
            test1.BoolValue = false;
            test1.GuidValue = Guid.NewGuid();
            test1.EnumValue = TestEnum.Value5;

            doc = test1.ConvertToBson();
            test2 = EntityToBsonController.ToEntity(doc, typeof(EntityTestAll)) as EntityTestAll;
            Assert.IsNull(test2.StringValue);
            Assert.IsNull(test2.BinaryValue);
            Assert.IsNull(test2.DictValue);
            Assert.IsNull(test2.DictValueRef);
            Assert.IsFalse(test2.BoolValue);
            Assert.AreEqual(TestEnum.Value5, test2.EnumValue);
        }

        [Test]
        public void TestBsonNullable()
        {
            DateTime dt = DateTime.Now;
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond);

            EntityTestAllNullable test1 = new EntityTestAllNullable(), test2;
            test1.BoolValue = true;
            test1.IntValue = 123;
            test1.LongValue = 345;
            test1.DoubleValue = 789.12;
            test1.DecimalValue = 678.91m;
            test1.GuidValue = Guid.NewGuid();
            test1.DateTimeValue = dt;
            test1.EnumValue = TestEnum.Value3;

            BsonDocument doc = test1.ConvertToBson();
            test2 = EntityToBsonController.ToEntity(doc, typeof(EntityTestAllNullable)) as EntityTestAllNullable;

            Assert.AreEqual(test1.BoolValue, test2.BoolValue);
            Assert.AreEqual(test1.IntValue, test2.IntValue);
            Assert.AreEqual(test1.LongValue, test2.LongValue);
            Assert.AreEqual(test1.DoubleValue, test2.DoubleValue);
            Assert.AreEqual(test1.DecimalValue, test2.DecimalValue);
            Assert.AreEqual(test1.GuidValue, test2.GuidValue);
            Assert.AreEqual(test1.DateTimeValue, test2.DateTimeValue);

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
            test2 = EntityToBsonController.ToEntity(doc, typeof(EntityTestAllNullable)) as EntityTestAllNullable;

            Assert.AreEqual(test1.BoolValue, test2.BoolValue);
            Assert.AreEqual(test1.IntValue, test2.IntValue);
            Assert.AreEqual(test1.LongValue, test2.LongValue);
            Assert.AreEqual(test1.DoubleValue, test2.DoubleValue);
            Assert.AreEqual(test1.DecimalValue, test2.DecimalValue);
            Assert.AreEqual(test1.GuidValue, test2.GuidValue);
            Assert.AreEqual(test1.DateTimeValue, test2.DateTimeValue);
        }

        [Test]
        public void TestBsonArray()
        {
            DateTime dt = DateTime.Now;
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond);

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
            test2 = EntityToBsonController.ToEntity(doc, typeof(EntityTestAllArr)) as EntityTestAllArr;

            Assert.AreEqual(test1.StringValue, test2.StringValue);
            Assert.AreEqual(test1.BoolValue, test2.BoolValue);
            Assert.AreEqual(test1.IntValue, test2.IntValue);
            Assert.AreEqual(test1.LongValue, test2.LongValue);
            Assert.AreEqual(test1.DoubleValue, test2.DoubleValue);
            Assert.AreEqual(test1.DecimalValue, test2.DecimalValue);
            Assert.AreEqual(test1.GuidValue, test2.GuidValue);
            Assert.AreEqual(test1.DateTimeValue, test2.DateTimeValue);
            Assert.AreEqual(test1.EnumValue, test2.EnumValue);
            Assert.AreEqual(test1.DictValue, test2.DictValue);

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
            test2 = EntityToBsonController.ToEntity(doc, typeof(EntityTestAllArr)) as EntityTestAllArr;

            Assert.AreEqual(test1.StringValue, test2.StringValue);
            Assert.AreEqual(test1.BoolValue, test2.BoolValue);
            Assert.AreEqual(test1.IntValue, test2.IntValue);
            Assert.AreEqual(test1.LongValue, test2.LongValue);
            Assert.AreEqual(test1.DoubleValue, test2.DoubleValue);
            Assert.AreEqual(test1.DecimalValue, test2.DecimalValue);
            Assert.AreEqual(test1.GuidValue, test2.GuidValue);
            Assert.AreEqual(test1.DateTimeValue, test2.DateTimeValue);
            Assert.AreEqual(test1.EnumValue, test2.EnumValue);
            Assert.AreEqual(test1.DictValue, test2.DictValue);

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
            test2 = EntityToBsonController.ToEntity(doc, typeof(EntityTestAllArr)) as EntityTestAllArr;

            Assert.AreEqual(test1.StringValue, test2.StringValue);
            Assert.AreEqual(test1.BoolValue, test2.BoolValue);
            Assert.AreEqual(test1.IntValue, test2.IntValue);
            Assert.AreEqual(test1.LongValue, test2.LongValue);
            Assert.AreEqual(test1.DoubleValue, test2.DoubleValue);
            Assert.AreEqual(test1.DecimalValue, test2.DecimalValue);
            Assert.AreEqual(test1.GuidValue, test2.GuidValue);
            Assert.AreEqual(test1.DateTimeValue, test2.DateTimeValue);
            Assert.AreEqual(test1.EnumValue, test2.EnumValue);
            Assert.AreEqual(test1.DictValue, test2.DictValue);
        }
    }
}