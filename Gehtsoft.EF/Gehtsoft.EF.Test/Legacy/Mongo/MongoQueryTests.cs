using System;
using AwesomeAssertions;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.MongoDb;
using Xunit;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Gehtsoft.EF.Test.Legacy.Mongo
{
    public class MongoQueryTests
    {
        [Fact]
        public void TestFilterBuilder()
        {
            BsonFilterExpressionBuilder builder = new BsonFilterExpressionBuilder();

            builder.Reset();
            builder.Add("a", CmpOp.Eq, 123);
            builder.EndGroup();
            Assert.Throws<EfMongoDbException>(() => builder.ToBsonDocument());

            builder.Reset();
            builder.BeginGroup(LogOp.And);
            Assert.Throws<EfMongoDbException>(() => builder.ToBsonDocument());

            builder.Reset();
            builder.BeginGroup(LogOp.And);
            Assert.Throws<EfMongoDbException>(() => builder.EndGroup());

            builder.Reset();
            builder.ToString().Should().Be("()");

            builder.Add("f1", CmpOp.Eq, 1);
            builder.Add("f2", CmpOp.Neq, 2);
            builder.Add("f3", CmpOp.Gt, 3);
            builder.Add("f4", CmpOp.Ge, 4);
            builder.Add("f5", CmpOp.Ls, 5);
            builder.Add("f6", CmpOp.Le, 6);
            builder.ToString().Should().Be("(f1 $eq 1 $and f2 $ne 2 $and f3 $gt 3 $and f4 $gte 4 $and f5 $lt 5 $and f6 $lte 6)");

            builder.Reset();
            builder.Add(LogOp.Or, "f1", CmpOp.Eq, 1);
            builder.Add(LogOp.Or, "f2", CmpOp.Neq, 2);
            builder.Add(LogOp.Or, "f3", CmpOp.Gt, 3);
            builder.ToString().Should().Be("(f1 $eq 1 $or f2 $ne 2 $or f3 $gt 3)");

            Assert.Throws<EfMongoDbException>(() => builder.Add(LogOp.And, "f4", CmpOp.Eq, 5));

            builder.Reset();
            builder.Add("f1", CmpOp.Eq, 1);
            builder.Add(LogOp.Or, "f2", CmpOp.Neq, 2);
            builder.Add(LogOp.Or, "f3", CmpOp.Gt, 3);
            builder.ToString().Should().Be("(f1 $eq 1 $or f2 $ne 2 $or f3 $gt 3)");

            builder.Reset();
            builder.Add("a", CmpOp.Eq, 1);
            builder.BeginGroup(LogOp.Or);

            builder.Add("b", CmpOp.Eq, 2);
            builder.BeginGroup(LogOp.And);

            builder.Add("c", CmpOp.Eq, 3);
            builder.Add(LogOp.Or, "d", CmpOp.Like, "a%");

            builder.EndGroup();
            builder.EndGroup();

            builder.ToString().Should().Be("(a $eq 1 $or (b $eq 2 $and (c $eq 3 $or d $regex a.*)))");

            builder.Reset();
            builder.Add("a", CmpOp.In, new int[] { 1, 2, 3 });
            builder.Add(LogOp.Or, "b", CmpOp.NotIn, new string[] { "a", "b", "c" });
            builder.Add(LogOp.Or, "c", CmpOp.IsNull, null);
            builder.Add(LogOp.Or, "d", CmpOp.NotNull, null);
            builder.ToString().Should().Be("(a $in [1, 2, 3] $or b $nin [a, b, c] $or c $eq BsonNull $or d $ne BsonNull)");
        }
    }
}
