using System;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Gehtsoft.EF.Bson;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.MongoDb;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Mongo
{
    public class Path
    {
        [Entity(Scope = "PathTranslator", NamingPolicy = EntityNamingPolicy.LowerCase)]
        public class EntityA
        {
            [EntityProperty]
            public string AA { get; set; }
            [EntityProperty]
            public string AB { get; set; }
        }

        [Entity(Scope = "PathTranslator", NamingPolicy = EntityNamingPolicy.LowerCase)]
        public class EntityB
        {
            [EntityProperty]
            public string BA { get; set; }

            [EntityProperty]
            public string[] BB { get; set; }

            [EntityProperty]
            public EntityA BC { get; set; }

            [EntityProperty]
            public EntityA[] BD { get; set; }
        }

        [Entity(Scope = "PathTranslator", NamingPolicy = EntityNamingPolicy.LowerCase)]
        public class EntityC
        {
            [EntityProperty]
            public string CA { get; set; }

            [EntityProperty]
            public EntityB CB { get; set; }
        }

        [Theory]
        [InlineData("CA", "ca")]
        [InlineData("CB.BA", "cb.ba")]
        [InlineData("CB.BB.3", "cb.bb.3")]
        [InlineData("CB.BC.AB", "cb.bc.ab")]
        [InlineData("CB.BD.157.AA", "cb.bd.157.aa")]
        public void Translate(string source, string result)
        {
            PathTranslator t = new PathTranslator(typeof(EntityC), AllEntities.Inst.FindBsonEntity(typeof(EntityC)));
            t.TranslatePath(source).Should().Be(result);
        }

        [Fact]
        public void NotAProperty1()
        {
            PathTranslator t = new PathTranslator(typeof(EntityC), AllEntities.Inst.FindBsonEntity(typeof(EntityC)));
            ((Action)(() => t.TranslatePath("CE"))).Should().Throw<EfMongoDbException>();
        }

        [Fact]
        public void NotAProperty2()
        {
            PathTranslator t = new PathTranslator(typeof(EntityC), AllEntities.Inst.FindBsonEntity(typeof(EntityC)));
            ((Action)(() => t.TranslatePath("CA.XX"))).Should().Throw<EfMongoDbException>();
        }

        [Fact]
        public void NotAnArray()
        {
            PathTranslator t = new PathTranslator(typeof(EntityC), AllEntities.Inst.FindBsonEntity(typeof(EntityC)));
            ((Action)(() => t.TranslatePath("CA.5"))).Should().Throw<EfMongoDbException>();
        }
    }
}
