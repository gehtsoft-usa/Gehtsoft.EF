using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.MongoDb;
using NUnit.Framework;

#pragma warning disable CS0618 // Type or member is obsolete

namespace TestApp
{
    [TestFixture]
    public class MongoTestQuery
    {
        [Test]
        public void TestFilterBuilder()
        {
            BsonFilterExpressionBuilder builder = new BsonFilterExpressionBuilder();

            builder.Reset();
            builder.Add("a", CmpOp.Eq, 123);
            builder.EndGroup();
            Assert.Throws<EfMongoDbException>(() => builder.ToBsonDocument(), "e2");

            builder.Reset();
            builder.BeginGroup(LogOp.And);
            Assert.Throws<EfMongoDbException>(() => builder.ToBsonDocument(), "e4");

            builder.Reset();
            builder.BeginGroup(LogOp.And);
            Assert.Throws<EfMongoDbException>(() => builder.EndGroup(), "e5");

            builder.Reset();
            Assert.AreEqual("()", builder.ToString(), "a1");

            builder.Add("f1", CmpOp.Eq, 1);
            builder.Add("f2", CmpOp.Neq, 2);
            builder.Add("f3", CmpOp.Gt, 3);
            builder.Add("f4", CmpOp.Ge, 4);
            builder.Add("f5", CmpOp.Ls, 5);
            builder.Add("f6", CmpOp.Le, 6);
            Assert.AreEqual("(f1 $eq 1 $and f2 $ne 2 $and f3 $gt 3 $and f4 $gte 4 $and f5 $lt 5 $and f6 $lte 6)", builder.ToString(), "a2");

            builder.Reset();
            builder.Add(LogOp.Or, "f1", CmpOp.Eq, 1);
            builder.Add(LogOp.Or, "f2", CmpOp.Neq, 2);
            builder.Add(LogOp.Or, "f3", CmpOp.Gt, 3);
            Assert.AreEqual("(f1 $eq 1 $or f2 $ne 2 $or f3 $gt 3)", builder.ToString(), "a3");

            Assert.Throws<EfMongoDbException>(() => builder.Add(LogOp.And, "f4", CmpOp.Eq, 5), "e5");

            builder.Reset();
            builder.Add("f1", CmpOp.Eq, 1);
            builder.Add(LogOp.Or, "f2", CmpOp.Neq, 2);
            builder.Add(LogOp.Or, "f3", CmpOp.Gt, 3);
            Assert.AreEqual("(f1 $eq 1 $or f2 $ne 2 $or f3 $gt 3)", builder.ToString(), "a4");

            builder.Reset();
            builder.Add("a", CmpOp.Eq, 1);
            builder.BeginGroup(LogOp.Or);

            builder.Add("b", CmpOp.Eq, 2);
            builder.BeginGroup(LogOp.And);

            builder.Add("c", CmpOp.Eq, 3);
            builder.Add(LogOp.Or, "d", CmpOp.Like, "a%");

            builder.EndGroup();
            builder.EndGroup();

            Assert.AreEqual("(a $eq 1 $or (b $eq 2 $and (c $eq 3 $or d $regex a.*)))", builder.ToString(), "a5");

            builder.Reset();
            builder.Add("a", CmpOp.In, new int[] { 1, 2, 3 });
            builder.Add(LogOp.Or, "b", CmpOp.NotIn, new string[] { "a", "b", "c" });
            builder.Add(LogOp.Or, "c", CmpOp.IsNull, null);
            builder.Add(LogOp.Or, "d", CmpOp.NotNull, null);
            Assert.AreEqual("(a $in [1, 2, 3] $or b $nin [a, b, c] $or c $eq BsonNull $or d $ne BsonNull)", builder.ToString());
        }
    }
}