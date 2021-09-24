using System;
using FluentAssertions;
using Gehtsoft.EF.Bson;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.MongoDb;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.Utils;
using MongoDB.Bson;
using Xunit;

namespace Gehtsoft.EF.Test.Mongo
{
    public class BsonValueConvert
    {
        [Theory]
        [InlineData(typeof(int), 10, typeof(BsonInt32), typeof(int), 10)]
        [InlineData(typeof(int), 10, typeof(BsonInt32), typeof(int?), 10)]
        [InlineData(typeof(object), null, typeof(BsonNull), typeof(int?), null)]
        [InlineData(typeof(object), null, typeof(BsonNull), typeof(int), 0)]
        [InlineData(typeof(long), 10, typeof(BsonInt64), typeof(long), 10)]
        [InlineData(typeof(long), 10, typeof(BsonInt64), typeof(long?), 10)]
        [InlineData(typeof(object), null, typeof(BsonNull), typeof(long?), null)]
        [InlineData(typeof(object), null, typeof(BsonNull), typeof(long), 0)]
        [InlineData(typeof(double), 1.234, typeof(BsonDouble), typeof(double), 1.234)]
        [InlineData(typeof(double), 1.234, typeof(BsonDouble), typeof(double?), 1.234)]
        [InlineData(typeof(object), null, typeof(BsonNull), typeof(double?), null)]
        [InlineData(typeof(object), null, typeof(BsonNull), typeof(double), 0)]
        [InlineData(typeof(Decimal128), 1.234, typeof(BsonDecimal128), typeof(decimal), 1.234)]
        [InlineData(typeof(Decimal128), 1.234, typeof(BsonDecimal128), typeof(decimal?), 1.234)]
        [InlineData(typeof(object), null, typeof(BsonNull), typeof(decimal?), null)]
        [InlineData(typeof(object), null, typeof(BsonNull), typeof(decimal), 0)]
        [InlineData(typeof(bool), true, typeof(BsonBoolean), typeof(bool), true)]
        [InlineData(typeof(bool), false, typeof(BsonBoolean), typeof(bool?), false)]
        [InlineData(typeof(object), null, typeof(BsonNull), typeof(bool?), null)]
        [InlineData(typeof(object), null, typeof(BsonNull), typeof(bool), false)]
        [InlineData(typeof(string), "abcd", typeof(BsonString), typeof(string), "abcd")]
        [InlineData(typeof(object), null, typeof(BsonNull), typeof(string), null)]
        [InlineData(typeof(byte[]), "01020304", typeof(BsonBinaryData), typeof(byte[]), "01020304")]
        [InlineData(typeof(object), null, typeof(BsonNull), typeof(byte[]), null)]
        [InlineData(typeof(string), "b6d7a749-23bb-4186-8956-19eecd12cf85", typeof(BsonString), typeof(Guid), "b6d7a749-23bb-4186-8956-19eecd12cf85")]
        [InlineData(typeof(string), "b6d7a749-23bb-4186-8956-19eecd12cf85", typeof(BsonString), typeof(Guid?), "b6d7a749-23bb-4186-8956-19eecd12cf85")]
        [InlineData(typeof(object), null, typeof(BsonNull), typeof(Guid?), null)]
        [InlineData(typeof(object), null, typeof(BsonNull), typeof(Guid), "00000000-0000-0000-0000-000000000000")]
        [InlineData(typeof(string), "", typeof(BsonString), typeof(Guid), "00000000-0000-0000-0000-000000000000")]
        [InlineData(typeof(string), "can'tparseit", typeof(BsonString), typeof(Guid), "00000000-0000-0000-0000-000000000000")]
        [InlineData(typeof(ObjectId), "507f1f77bcf86cd799439011", typeof(BsonObjectId), typeof(ObjectId), "507f1f77bcf86cd799439011")]
        [InlineData(typeof(ObjectId), "507f1f77bcf86cd799439011", typeof(BsonObjectId), typeof(ObjectId?), "507f1f77bcf86cd799439011")]
        [InlineData(typeof(object), null, typeof(BsonNull), typeof(ObjectId?), null)]
        [InlineData(typeof(object), null, typeof(BsonNull), typeof(DateTime?), null)]
        [InlineData(typeof(object), null, typeof(BsonNull), typeof(int[]), null)]

        public void SimpleConversion(Type sourceType, object sourceValue, Type bsonType, Type requestType, object expectedValue)
        {
            sourceValue = TestValue.Translate(sourceType, sourceValue);
            expectedValue = TestValue.Translate(requestType, expectedValue);

            BsonValue value;
            
            if (sourceValue == null && bsonType == typeof(BsonNull))
                value = BsonNull.Value;
            else
            {
                var constructor = bsonType.GetConstructor(new[] { sourceType });
                constructor.Should().NotBeNull();
                value = (BsonValue)constructor.Invoke(new[] { sourceValue });
            }

            if (requestType == typeof(byte[]))
                value.ConvertTo(requestType).Should()
                    .BeEquivalentTo(expectedValue);
            else
                value.ConvertTo(requestType).Should()
                    .Be(expectedValue);
        }

        [Entity(Scope = "valueConversion")]
        public class Entity
        {
            [AutoId()]
            public ObjectId ID { get; set; }

            [EntityProperty()]
            public string A { get; set; }
        }

        [Fact]
        public void ToEntity()
        {
            BsonDocument doc = new BsonDocument
            {
                { "_id", new BsonObjectId(new ObjectId("507f1f77bcf86cd799439011")) },
                { "a", new BsonString("abcd") }
            };

            var e = doc.ConvertTo(typeof(Entity));
            e.Should()
                .BeOfType<Entity>();

            e.As<Entity>().ID.Should().Be(new ObjectId("507f1f77bcf86cd799439011"));
            e.As<Entity>().A.Should().Be("abcd");
        }

        [Fact]
        public void ToDocument()
        {
            BsonDocument doc = new BsonDocument
            {
                { "_id", new BsonObjectId(new ObjectId("507f1f77bcf86cd799439011")) },
                { "a", new BsonString("abcd") }
            };

            var e = doc.ConvertTo(typeof(BsonDocument));

            e.Should()
                .BeOfType<BsonDocument>();

            e.As<BsonDocument>()
                .Should()
                .HavePropertiesCount(2)
                .And.HaveProperty("_id", new ObjectId("507f1f77bcf86cd799439011"))
                .And.HaveProperty("a", "abcd");
        }

        [Fact]
        public void Date()
        {
            DateTime dt = new DateTime(2010, 11, 22, 13, 55, 17, 305, DateTimeKind.Local).ToLocalTime();
            
            BsonValue v = new BsonDateTime(dt.ToUniversalTime());

            v.ConvertTo(typeof(object))
                .Should()
                .BeOfType<DateTime>()
                .And.Be(dt.ToUniversalTime());

            EntityToBsonController.ReturnDateTimeAsLocalByDefault = false;
            v.ConvertTo(typeof(DateTime))
                .Should().Be(dt.ToUniversalTime());

            v.ConvertTo(typeof(DateTime?))
                .Should().Be(dt.ToUniversalTime());

            EntityToBsonController.ReturnDateTimeAsLocalByDefault = true;
            v.ConvertTo(typeof(DateTime))
                .Should().Be(dt);
        }

        [Fact]
        public void ArrayOfPrimitives()
        {
            BsonArray arr = new BsonArray()
            {
                new BsonInt32(1),
                new BsonInt32(2),
                new BsonInt32(3),
            };

            var e = arr.ConvertTo(typeof(int[]));
            e.Should().BeOfType<int[]>();
            e.As<int[]>()
                .Should().HaveCount(3)
                .And.HaveElementMatchingAt(0, x => x == 1)
                .And.HaveElementMatchingAt(1, x => x == 2)
                .And.HaveElementMatchingAt(2, x => x == 3);
        }
    }
}
