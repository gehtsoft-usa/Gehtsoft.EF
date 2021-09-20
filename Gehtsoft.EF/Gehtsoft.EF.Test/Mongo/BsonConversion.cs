using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Gehtsoft.EF.Bson;
using Xunit;
using FluentAssertions;
using MongoDB.Bson;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Gehtsoft.EF.Test.Mongo
{
    public class BsonConversion
    {
        [Entity(Scope = "BsonTest")]
        public class TestEntity<T>
        {
            [EntityProperty]
            public T Value { get; set; }
        }

        [Theory]
        [InlineData(typeof(int), 1, nameof(BsonValue.AsInt32))]
        [InlineData(typeof(int?), 1, nameof(BsonValue.AsInt32))]
        [InlineData(typeof(int?), null, nameof(BsonValue.AsBsonNull), typeof(BsonNull), null)]
        [InlineData(typeof(long), 1, nameof(BsonValue.AsInt64))]
        [InlineData(typeof(bool), true, nameof(BsonValue.AsBoolean))]
        [InlineData(typeof(bool), false, nameof(BsonValue.AsBoolean))]
        [InlineData(typeof(double), 1.23, nameof(BsonValue.AsDouble))]
        [InlineData(typeof(decimal), 1.23, nameof(BsonValue.AsDecimal))]
        [InlineData(typeof(string), "abcd", nameof(BsonValue.AsString))]
        [InlineData(typeof(string), null, nameof(BsonValue.AsBsonNull), typeof(BsonNull), null)]
        [InlineData(typeof(Guid), "c25f12a3-36fb-4263-be31-773f675d9aa9", nameof(BsonValue.AsString), typeof(string), "c25f12a3-36fb-4263-be31-773f675d9aa9")]
        [InlineData(typeof(Guid?), "c25f12a3-36fb-4263-be31-773f675d9aa9", nameof(BsonValue.AsString), typeof(string), "c25f12a3-36fb-4263-be31-773f675d9aa9")]
        [InlineData(typeof(DateTime), "2021-12-27 12:55:17Z", nameof(BsonValue.ToUniversalTime))]
        [InlineData(typeof(ObjectId), "507f191e810c19729de860ea", nameof(BsonValue.AsObjectId))]
        [InlineData(typeof(byte[]), "507f191e810c19729de860ea", nameof(BsonValue.AsBsonBinaryData), typeof(BsonBinaryData), "507f191e810c19729de860ea")]
        public void ToBson_RoundTrip(Type type, object value, string getPropertyName, Type expectedType = null, object expectedValue = null)
        {
            EntityToBsonController.ReturnDateTimeAsLocalByDefault = false;

            value = TestValue.Translate(type, value);
            if (expectedType != null)
                expectedValue = TestValue.Translate(expectedType, expectedValue);
            else
                expectedValue = value;

            var entityType = typeof(TestEntity<>).MakeGenericType(new Type[] { type });
            var entity = Activator.CreateInstance(entityType);
            var property = entityType.GetProperty("Value");
            property.SetValue(entity, value);

            var doc = entity.ConvertToBson();
            doc.ElementCount.Should().Be(1);
            var bsonProperty = doc.Elements.First();
            bsonProperty.Name.Should().Be("value");
            var property1 = bsonProperty.Value.GetType().GetProperty(getPropertyName);

            object value1;
            if (property1 == null)
            {
                var method1 = bsonProperty.Value.GetType().GetMethod(getPropertyName);
                method1.Should().NotBeNull();
                method1.IsStatic.Should().BeFalse();
                method1.GetParameters().Should().BeNullOrEmpty();
                value1 = method1.Invoke(bsonProperty.Value, Array.Empty<object>());
            }
            else
                value1 = property1.GetValue(bsonProperty.Value);

            value1.Should().Be(expectedValue);

            var entity1 = doc.ToEntity(entityType);
            property.GetValue(entity1).Should().Be(value);
        }

        [Entity(Scope = "BsonTest")]
        public class Entity1
        {
            [AutoId]
            public ObjectId Id { get; set; }

            [EntityProperty (Field = "sv", Nullable = true)]
            public string StringValue { get; set; }

            [EntityProperty(Field = "dt", Nullable = true)]
            public DateTime? DateTimeValue { get; set; }
            
            [EntityProperty(Field = "en", Nullable = true)]
            public DayOfWeek? DayOfWeek { get; set; }
        }

        [Fact]
        public void Enity1Roundtrip_AllFilled_UnspecifiedDateKind()
        {
            EntityToBsonController.UnspecifiedTypeIsLocalByDefault = true;
            EntityToBsonController.ReturnDateTimeAsLocalByDefault = true;

            var e = new Entity1()
            {
                Id = new ObjectId("507f191e810c19729de860ea"),
                StringValue = "abcd",
                DateTimeValue = new DateTime(2020, 11, 25, 11, 22, 44, DateTimeKind.Unspecified),
                DayOfWeek = DayOfWeek.Saturday
            };

            var bson = e.ConvertToBson();
            bson.Elements.Should().HaveCount(4);
            bson["_id"].AsObjectId.Should().Be(e.Id);
            bson["sv"].AsString.Should().Be(e.StringValue);
            bson["en"].AsInt32.Should().Be((int)DayOfWeek.Saturday);
            bson["dt"].ToUniversalTime().Should().Be(new DateTime(2020, 11, 25, 11, 22, 44, DateTimeKind.Local).ToUniversalTime());

            var e1 = bson.ToEntity<Entity1>();
            e1.Id.Should().Be(e.Id);
            e1.StringValue.Should().Be(e.StringValue);
            e1.DateTimeValue.Should().Be(new DateTime(2020, 11, 25, 11, 22, 44, DateTimeKind.Local));
        }

        [Fact]
        public void Enity1Roundtrip_AllNull()
        {
            EntityToBsonController.UnspecifiedTypeIsLocalByDefault = true;
            EntityToBsonController.ReturnDateTimeAsLocalByDefault = true;

            var e = new Entity1()
            {
                StringValue = null,
                DateTimeValue = null,
                DayOfWeek = null,
            };

            var bson = e.ConvertToBson();
            bson.Elements.Should().HaveCount(4);
            bson["_id"].AsObjectId.Should().Be(ObjectId.Empty);
            bson["sv"].IsBsonNull.Should().Be(true);
            bson["dt"].IsBsonNull.Should().Be(true);
            bson["en"].IsBsonNull.Should().Be(true);

            var e1 = bson.ToEntity<Entity1>();
            e1.Id.Should().Be(ObjectId.Empty);
            e1.StringValue.Should().BeNull();
            e1.DateTimeValue.Should().BeNull();
            e1.DayOfWeek.Should().BeNull();
        }

        [Entity(Scope = "BsonTest")]
        public class Entity2<T>
        {
            [EntityProperty(Field = "arr")]
            public T[] Array { get; set; }

        }

        [Fact]
        public void Entity2RoundType_SimpleArray_Int()
        {
            var e = new Entity2<int>();
            e.Array = new int[32];
            for (int i = 0; i < 32; i++)
                e.Array[i] = i * 2;

            var bson = e.ConvertToBson();
            var arr = bson["arr"];
            arr.IsBsonArray.Should().BeTrue();
            arr.AsBsonArray.Count.Should().Be(32);

            var e1 = bson.ToEntity<Entity2<int>>();
            e1.Array.Should()
                .NotBeNull()
                .And.HaveCount(32);

            for (int i = 0; i < 32; i++)
                e1.Array[i].Should().Be(i * 2);
        }

        [Fact]
        public void Entity2RoundType_SimpleArray_Empty()
        {
            var e = new Entity2<int>();
            e.Array = new int[0];

            var bson = e.ConvertToBson();
            var arr = bson["arr"];
            arr.IsBsonArray.Should().BeTrue();
            arr.AsBsonArray.Count.Should().Be(0);

            var e1 = bson.ToEntity<Entity2<int>>();
            e1.Array.Should()
                .NotBeNull()
                .And.HaveCount(0);
        }

        [Fact]
        public void Entity2RoundType_SimpleArray_Null()
        {
            var e = new Entity2<int>();
            e.Array = null;

            var bson = e.ConvertToBson();
            var arr = bson["arr"];
            arr.IsBsonNull.Should().BeTrue();

            var e1 = bson.ToEntity<Entity2<int>>();
            e1.Array.Should()
                .BeNull();
        }

        [Entity(Scope = "BsonTest")]
        public class Aggregate
        {
            [EntityProperty(Field = "name")]
            public string Name { get; set; }
        }

        [Entity(Scope = "BsonTest")]
        public class Dict
        {
            [AutoId]
            public ObjectId Id { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        }

        [Entity(Scope = "BsonTest")]
        public class Aggregator
        {
            [EntityProperty(Field = "name")]
            public string Name { get; set; }

            [EntityProperty(Field = "aggregate")]
            public Aggregate Aggregate { get; set; }
        }

        [Entity(Scope = "BsonTest")]
        public class Aggregator1
        {
            [EntityProperty(Field = "name")]
            public string Name { get; set; }

            [EntityProperty(Field = "aggregates")]
            public Aggregate[] Aggregates { get; set; }
        }

        [Entity(Scope = "BsonTest")]
        public class ReferenceTo
        {
            [EntityProperty(Field = "name")]
            public string Name { get; set; }

            [EntityProperty(Field = "dict", ForeignKey = true)]
            public Dict Dict { get; set; }
        }

        [Fact]
        public void SimpleReference_Roundtrip_Filled()
        {
            var agg = new Aggregator()
            {
                Name = "name1",
                Aggregate = new Aggregate() { Name = "name2" }
            };

            var doc = agg.ConvertToBson();
            doc.Elements.Should().HaveCount(2);
            doc["name"].AsString.Should().Be("name1");
            doc["aggregate"].IsBsonDocument.Should().BeTrue();
            doc["aggregate"].AsBsonDocument.Should().HaveCount(1);
            doc["aggregate"].AsBsonDocument["name"].AsString.Should().Be("name2");

            var e1 = doc.ToEntity<Aggregator>();
            e1.Name.Should().Be("name1");
            e1.Aggregate.Should().NotBeNull();
            e1.Aggregate.Name.Should().Be("name2");
        }

        [Fact]
        public void SimpleReference_Roundtrip_Null()
        {
            var agg = new Aggregator()
            {
                Name = "name1",
                Aggregate = null
            };

            var doc = agg.ConvertToBson();
            doc.Elements.Should().HaveCount(2);
            doc["name"].AsString.Should().Be("name1");
            doc["aggregate"].IsBsonNull.Should().BeTrue();
            var e1 = doc.ToEntity<Aggregator>();
            e1.Name.Should().Be("name1");
            e1.Aggregate.Should().BeNull();
        }

        [Fact]
        public void ArrayOfReferences()
        {
            var agg = new Aggregator1()
            {
                Name = "name1",
                Aggregates = new[]
                {
                    new Aggregate() { Name = "name21" },
                    new Aggregate() { Name = "name22" },
                    new Aggregate() { Name = "name23" }
                }
            };
            var doc = agg.ConvertToBson();
            doc.Elements.Should().HaveCount(2);
            doc["name"].AsString.Should().Be("name1");
            doc["aggregates"].IsBsonArray.Should().BeTrue();
            doc["aggregates"].AsBsonArray.Should().HaveCount(3);

            var e1 = doc.ToEntity<Aggregator1>();
            e1.Name.Should().Be("name1");
            e1.Aggregates.Should().NotBeNull();
            e1.Aggregates.Should().HaveCount(3);
            e1.Aggregates[0].Name.Should().Be("name21");
            e1.Aggregates[1].Name.Should().Be("name22");
            e1.Aggregates[2].Name.Should().Be("name23");
        }

        [Fact]
        public void Reference()
        {
            var agg = new ReferenceTo()
            {
                Name = "name1",
                Dict = new Dict() { Id = ObjectId.GenerateNewId(), Name = "name2" }
            };

            var doc = agg.ConvertToBson();
            doc.Elements.Should().HaveCount(2);
            doc["name"].AsString.Should().Be("name1");
            doc["dict"].IsObjectId.Should().BeTrue();
            doc["dict"].AsObjectId.Should().Be(agg.Dict.Id);

            var e1 = doc.ToEntity<ReferenceTo>();
            e1.Name.Should().Be("name1");
            e1.Dict.Should().NotBeNull();
            e1.Dict.Id.Should().Be(e1.Dict.Id);
        }

        [Fact]
        public void CollectionDeserialization()
        {
            var list = new List<BsonDocument>();
            for (int i = 0; i < 5; i++)
            {
                var d = new BsonDocument();
                d.Add(new BsonElement("name", new BsonString($"name{i + 1}")));
                list.Add(d);
            }

            var es = list.ToEntities<Aggregate>();
            es.Should().NotBeNull().And.HaveCount(5);
            es[0].Name.Should().Be("name1");
            es[1].Name.Should().Be("name2");
            es[2].Name.Should().Be("name3");
            es[3].Name.Should().Be("name4");
            es[4].Name.Should().Be("name5");
            es[0].Name.Should().Be("name1");
        }
    }
}