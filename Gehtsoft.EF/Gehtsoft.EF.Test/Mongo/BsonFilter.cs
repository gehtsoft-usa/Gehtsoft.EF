using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.MongoDb;
using Gehtsoft.EF.Test.Utils;
using MongoDB.Bson;
using Moq;
using Xunit;

namespace Gehtsoft.EF.Test.Mongo
{
    public class BsonFilter
    {
        [Theory]
        [InlineData(CmpOp.Eq, "$eq", 5, 5)]
        [InlineData(CmpOp.Neq, "$ne", 1.23, 1.23)]
        [InlineData(CmpOp.Le, "$lte", "abc", "abc")]
        [InlineData(CmpOp.Ls, "$lt", true, true)]
        [InlineData(CmpOp.Ge, "$gte", 5, 5)]
        [InlineData(CmpOp.Gt, "$gt", 5, 5)]
        public void BitwiseOp_Direct(CmpOp op, string expectedOp, object arg, object expectedArg)
        {
            var resolver = new Mock<IMongoPathResolver>();
            resolver.Setup(r => r.TranslatePath(It.IsAny<string>()))
                .Returns<string>(a => a);
            var builder = new BsonFilterExpressionBuilder();

            builder.Add("a", op, arg);
            var doc = builder
                .ToBsonDocument()
                .ToBsonDocument()["Document"].AsBsonDocument;

            doc.Should()
                .HaveProperty("a");

            var v1 = doc["a"];
            v1.Should().HavePropertiesCount(1)
                .And.HaveProperty(expectedOp, expectedArg);
        }

        [Theory]
        [InlineData(CmpOp.Eq, "$eq", 5, 5)]
        [InlineData(CmpOp.Neq, "$ne", 1.23, 1.23)]
        [InlineData(CmpOp.Le, "$lte", "abc", "abc")]
        [InlineData(CmpOp.Ls, "$lt", true, true)]
        [InlineData(CmpOp.Ge, "$gte", 5, 5)]
        [InlineData(CmpOp.Gt, "$gt", 5, 5)]
        [InlineData(CmpOp.Like, "$regex", "a%.[a-e]b", "a.*.[a-e]b")]
        [InlineData(CmpOp.Like, "$regex", "/a%.+b/", "a%.+b")]
        public void BitwiseOp_Is(CmpOp op, string expectedOp, object arg, object expectedArg)
        {
            var resolver = new Mock<IMongoPathResolver>();
            resolver.Setup(r => r.TranslatePath(It.IsAny<string>()))
                .Returns<string>(a => a);
            var builder = new BsonFilterExpressionBuilder();
            var condition = new MongoQueryCondition(resolver.Object, builder);

            condition.Property("a").Is(op).Value(arg);
            var doc = builder
                .ToBsonDocument()
                .ToBsonDocument()["Document"].AsBsonDocument;

            doc.Should()
                .HaveProperty("a");

            var v1 = doc["a"];
            v1.Should().HavePropertiesCount(1)
                .And.HaveProperty(expectedOp, expectedArg);
        }

        [Theory]
        [InlineData(CmpOp.Eq, "$eq", 5, 5)]
        [InlineData(CmpOp.Neq, "$ne", 1.23, 1.23)]
        [InlineData(CmpOp.Le, "$lte", "abc", "abc")]
        [InlineData(CmpOp.Ls, "$lt", true, true)]
        [InlineData(CmpOp.Ge, "$gte", 5, 5)]
        [InlineData(CmpOp.Gt, "$gt", 5, 5)]
        [InlineData(CmpOp.Like, "$regex", "a", "a")]
        public void BitwiseOp_Extension1(CmpOp op, string expectedOp, object arg, object expectedArg)
        {
            var resolver = new Mock<IMongoPathResolver>();
            resolver.Setup(r => r.TranslatePath(It.IsAny<string>()))
                .Returns<string>(a => a);
            var builder = new BsonFilterExpressionBuilder();
            var condition = new MongoQueryCondition(resolver.Object, builder);

            var m = typeof(MongoQuerySingleConditionBuilderExtension)
                .GetMethod(op.ToString(), new[] { typeof(MongoQuerySingleConditionBuilder) });

            var c = condition.Property("a");
            m.Invoke(null, new[] { c });
            c.Value(arg);
            var doc = builder
                .ToBsonDocument()
                .ToBsonDocument()["Document"].AsBsonDocument;

            doc.Should()
                .HaveProperty("a");

            var v1 = doc["a"];
            v1.Should().HavePropertiesCount(1)
                .And.HaveProperty(expectedOp, expectedArg);
        }

        [Theory]
        [InlineData(CmpOp.Eq, "$eq", 5, 5)]
        [InlineData(CmpOp.Neq, "$ne", 1.23, 1.23)]
        [InlineData(CmpOp.Le, "$lte", "abc", "abc")]
        [InlineData(CmpOp.Ls, "$lt", true, true)]
        [InlineData(CmpOp.Ge, "$gte", 5, 5)]
        [InlineData(CmpOp.Gt, "$gt", 5, 5)]
        public void BitwiseOp_Extension2(CmpOp op, string expectedOp, object arg, object expectedArg)
        {
            var resolver = new Mock<IMongoPathResolver>();
            resolver.Setup(r => r.TranslatePath(It.IsAny<string>()))
                .Returns<string>(a => a);
            var builder = new BsonFilterExpressionBuilder();
            var condition = new MongoQueryCondition(resolver.Object, builder);

            var m = typeof(MongoQuerySingleConditionBuilderExtension)
                .GetMethod(op.ToString(), new[] { typeof(MongoQuerySingleConditionBuilder), typeof(object) });

            var c = condition.Property("a");
            m.Invoke(null, new[] { c, arg });

            var doc = builder
                .ToBsonDocument()
                .ToBsonDocument()["Document"].AsBsonDocument;

            doc.Should()
                .HaveProperty("a");

            var v1 = doc["a"];
            v1.Should().HavePropertiesCount(1)
                .And.HaveProperty(expectedOp, expectedArg);
        }

        [Theory]
        [InlineData(CmpOp.In, "$in")]
        [InlineData(CmpOp.NotIn, "$nin")]
        public void In_Is(CmpOp op, string expectedOp)
        {
            var resolver = new Mock<IMongoPathResolver>();
            resolver.Setup(r => r.TranslatePath(It.IsAny<string>()))
                .Returns<string>(a => a);
            var builder = new BsonFilterExpressionBuilder();
            var condition = new MongoQueryCondition(resolver.Object, builder);

            condition.Property("a").Is(op).Value(new object[] { 1, "abc", true });
            var doc = builder
                .ToBsonDocument()
                .ToBsonDocument()["Document"].AsBsonDocument;

            doc.Should()
                .HaveProperty("a");

            var v1 = doc["a"];
            v1.Should()
                .HavePropertiesCount(1)
                .And.HaveProperty(expectedOp)
                .And.Subject[expectedOp].Should()
                    .BeArray()
                    .And.HaveCount(3)
                    .And.HaveElement(0, 1)
                    .And.HaveElement(1, "abc")
                    .And.HaveElement(2, true);
        }

        [Theory]
        [InlineData(CmpOp.In, "$in")]
        [InlineData(CmpOp.NotIn, "$nin")]
        public void In_Extension1(CmpOp op, string expectedOp)
        {
            var resolver = new Mock<IMongoPathResolver>();
            resolver.Setup(r => r.TranslatePath(It.IsAny<string>()))
                .Returns<string>(a => a);
            var builder = new BsonFilterExpressionBuilder();
            var condition = new MongoQueryCondition(resolver.Object, builder);

            var m = typeof(MongoQuerySingleConditionBuilderExtension)
                .GetMethod(op.ToString(), new[] { typeof(MongoQuerySingleConditionBuilder) });

            var c = condition.Property("a");
            m.Invoke(null, new[] { c });
            c.Value(new object[] { 1, "abc", true });

            var doc = builder
                .ToBsonDocument()
                .ToBsonDocument()["Document"].AsBsonDocument;

            doc.Should()
                .HaveProperty("a");

            var v1 = doc["a"];
            v1.Should()
                .HavePropertiesCount(1)
                .And.HaveProperty(expectedOp)
                .And.Subject[expectedOp].Should()
                    .BeArray()
                    .And.HaveCount(3)
                    .And.HaveElement(0, 1)
                    .And.HaveElement(1, "abc")
                    .And.HaveElement(2, true);
        }

        [Theory]
        [InlineData(CmpOp.In, "$in")]
        [InlineData(CmpOp.NotIn, "$nin")]
        public void In_Extension2(CmpOp op, string expectedOp)
        {
            var resolver = new Mock<IMongoPathResolver>();
            resolver.Setup(r => r.TranslatePath(It.IsAny<string>()))
                .Returns<string>(a => a);
            var builder = new BsonFilterExpressionBuilder();
            var condition = new MongoQueryCondition(resolver.Object, builder);

            var m = typeof(MongoQuerySingleConditionBuilderExtension)
                .GetMethod(op.ToString(), new[] { typeof(MongoQuerySingleConditionBuilder), typeof(object[]) });

            var c = condition.Property("a");
            m.Invoke(null, new object[] { c, new object[] { 1, "abc", true } } );

            var doc = builder
                .ToBsonDocument()
                .ToBsonDocument()["Document"].AsBsonDocument;

            doc.Should()
                .HaveProperty("a");

            var v1 = doc["a"];
            v1.Should()
                .HavePropertiesCount(1)
                .And.HaveProperty(expectedOp)
                .And.Subject[expectedOp].Should()
                    .BeArray()
                    .And.HaveCount(3)
                    .And.HaveElement(0, 1)
                    .And.HaveElement(1, "abc")
                    .And.HaveElement(2, true);
        }

        [Theory]
        [InlineData(CmpOp.IsNull, "$eq")]
        [InlineData(CmpOp.NotNull, "$ne")]
        public void IsNull_Is(CmpOp op, string expectedOp)
        {
            var resolver = new Mock<IMongoPathResolver>();
            resolver.Setup(r => r.TranslatePath(It.IsAny<string>()))
                .Returns<string>(a => a);
            var builder = new BsonFilterExpressionBuilder();
            var condition = new MongoQueryCondition(resolver.Object, builder);

            condition.Property("a").Is(op);

            var doc = builder
                .ToBsonDocument()
                .ToBsonDocument()["Document"].AsBsonDocument;

            doc.Should()
                .HaveProperty("a");

            var v1 = doc["a"];
            v1.Should().HavePropertiesCount(1)
                .And.HaveProperty(expectedOp, null);
        }

        [Fact]
        public void Error_Property_MustBeFirst()
        {
            var resolver = new Mock<IMongoPathResolver>();
            resolver.Setup(r => r.TranslatePath(It.IsAny<string>()))
                .Returns<string>(a => a);
            var builder = new BsonFilterExpressionBuilder();
            var condition = new MongoQueryCondition(resolver.Object, builder);

            ((Action)(() => condition.Add(LogOp.And).Property("a").Eq().Property("b"))).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Error_Value_MustBeSecond()
        {
            var resolver = new Mock<IMongoPathResolver>();
            resolver.Setup(r => r.TranslatePath(It.IsAny<string>()))
                .Returns<string>(a => a);
            var builder = new BsonFilterExpressionBuilder();
            var condition = new MongoQueryCondition(resolver.Object, builder);

            ((Action)(() => condition.Add(LogOp.And).Value("a").Property("b"))).Should().Throw<InvalidOperationException>();
            ((Action)(() => condition.Add(LogOp.And).Property("a").Value("b"))).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Error_Is_MustBeSecond()
        {
            var resolver = new Mock<IMongoPathResolver>();
            resolver.Setup(r => r.TranslatePath(It.IsAny<string>()))
                .Returns<string>(a => a);
            var builder = new BsonFilterExpressionBuilder();
            var condition = new MongoQueryCondition(resolver.Object, builder);

            ((Action)(() => condition.Add(LogOp.And).Eq().Property("b"))).Should().Throw<InvalidOperationException>();
        }

        [Theory]
        [InlineData(LogOp.And, "$and")]
        [InlineData(LogOp.Or, "$or")]
        public void LogOp_Method1(LogOp logOp, string expectedOp)
        {
            var resolver = new Mock<IMongoPathResolver>();
            resolver.Setup(r => r.TranslatePath(It.IsAny<string>()))
                .Returns<string>(a => a);
            var builder = new BsonFilterExpressionBuilder();
            var condition = new MongoQueryCondition(resolver.Object, builder);

            condition.Add(logOp).Property("a").Eq(5);
            condition.Add(logOp).Property("b").Eq(6);

            var doc = builder
                .ToBsonDocument()
                .ToBsonDocument()["Document"].AsBsonDocument;

            doc.Should()
                .HaveProperty(expectedOp)
                .And.Subject[expectedOp].Should()
                    .BeArray()
                    .And.HaveCount(2);

            var arr = doc[expectedOp].AsBsonArray;

            arr[0]
                .Should()
                .BeDocument()
                .And.HaveProperty("a");

            arr[1]
                .Should()
                .BeDocument()
                .And.HaveProperty("b");
        }

        [Theory]
        [InlineData(LogOp.And, "$and")]
        [InlineData(LogOp.Or, "$or")]
        public void LogOp_Method2(LogOp logOp, string expectedOp)
        {
            var resolver = new Mock<IMongoPathResolver>();
            resolver.Setup(r => r.TranslatePath(It.IsAny<string>()))
                .Returns<string>(a => a);
            var builder = new BsonFilterExpressionBuilder();
            var condition = new MongoQueryCondition(resolver.Object, builder);

            condition.Add(logOp).Property("a").Eq(5);

            var m = typeof(MongoQueryConditionExtension)
                .GetMethod(logOp.ToString(), new[] { typeof(MongoQueryCondition) });
            var s = m.Invoke(null, new object[] { condition }) as MongoQuerySingleConditionBuilder;
            s.Property("b").Eq(6);

            var doc = builder
                .ToBsonDocument()
                .ToBsonDocument()["Document"].AsBsonDocument;

            doc.Should()
                .HaveProperty(expectedOp)
                .And.Subject[expectedOp].Should()
                    .BeArray()
                    .And.HaveCount(2);

            var arr = doc[expectedOp].AsBsonArray;

            arr[0]
                .Should()
                .BeDocument()
                .And.HaveProperty("a");

            arr[1]
                .Should()
                .BeDocument()
                .And.HaveProperty("b");
        }

        [Fact]
        public void Error_LogOp_ShouldBeSame()
        {
            var resolver = new Mock<IMongoPathResolver>();
            resolver.Setup(r => r.TranslatePath(It.IsAny<string>()))
                    .Returns<string>(a => a);
            var builder = new BsonFilterExpressionBuilder();
            var condition = new MongoQueryCondition(resolver.Object, builder);

            condition.Add(LogOp.And).Property("a").Eq(5);
            ((Action)(() => condition.Add(LogOp.Or).Property("a").Eq(5))).Should().Throw<EfMongoDbException>();
        }

        private static void ValidateGroup(BsonDocument doc)
        {
            doc.Should()
                .HaveProperty("$and")
                .And.Subject["$and"].Should()
                    .BeArray()
                    .And.HaveCount(2);

            var and = doc["$and"].AsBsonArray;

            and.Should()
                .HaveCount(2);

            and[0]
                .Should()
                .BeDocument()
                .And.HaveProperty("a");

            and[0]["a"]
                .Should()
                .BeDocument()
                .And.HaveProperty("$eq", 5);

            and[1]
                .Should()
                .BeDocument()
                .And.HaveProperty("$or");

            var or = and[1]["$or"];

            or.Should()
                .BeArray()
                .And.HaveCount(3);

            or[0]
                .Should()
                .BeDocument()
                .And.HaveProperty("b")
                .And.Subject["b"]
                    .Should()
                    .HaveProperty("$eq", 6);

            or[1]
                .Should()
                .BeDocument()
                .And.HaveProperty("c")
                .And.Subject["c"]
                    .Should()
                    .HaveProperty("$eq", 7);

            or[2]
                .Should()
                .BeDocument()
                .And.HaveProperty("d")
                .And.Subject["d"]
                    .Should()
                    .HaveProperty("$eq", 8);
        }

        [Fact]
        public void AddGroup_Method1()
        {
            var resolver = new Mock<IMongoPathResolver>();
            resolver.Setup(r => r.TranslatePath(It.IsAny<string>()))
                    .Returns<string>(a => a);
            var builder = new BsonFilterExpressionBuilder();
            var condition = new MongoQueryCondition(resolver.Object, builder);

            condition.Property("a").Eq(5);
            using (var g = condition.AddGroup(LogOp.And))
            {
                condition.Or().Property("b").Eq(6);
                condition.Or().Property("c").Eq(7);
                condition.Or().Property("d").Eq(8);
            }

            var doc = builder
                .ToBsonDocument()
                .ToBsonDocument()["Document"].AsBsonDocument;

            doc.Should().NotBeNull();
            ValidateGroup(doc);
        }

        [Fact]
        public void AddGroup_Method2()
        {
            var resolver = new Mock<IMongoPathResolver>();
            resolver.Setup(r => r.TranslatePath(It.IsAny<string>()))
                    .Returns<string>(a => a);
            var builder = new BsonFilterExpressionBuilder();
            var condition = new MongoQueryCondition(resolver.Object, builder);

            condition.Property("a").Eq(5);
            condition.And(g => {
                condition.Or().Property("b").Eq(6);
                condition.Or().Property("c").Eq(7);
                condition.Or().Property("d").Eq(8);
            });

            var doc = builder
                .ToBsonDocument()
                .ToBsonDocument()["Document"].AsBsonDocument;

            doc.Should().NotBeNull();
            ValidateGroup(doc);
        }
    }
}
