using System;
using AwesomeAssertions;
using Gehtsoft.EF.Bson;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.Entities
{
    public class DynamicPropertiesBsonExclusionTest
    {
        [Entity(Scope = "dynprops_bson")]
        public class BsonPlainEntity
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        }

        [Entity(Scope = "dynprops_bson")]
        [DynamicProperties]
        public class BsonDynPropsEntity
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        }

        [Fact]
        public void DynamicPropertiesEntity_IsRejectedByBsonLayer()
        {
            Action action = () => AllEntities.Inst.FindBsonEntity(typeof(BsonDynPropsEntity));

            action.Should()
                  .Throw<BsonException>()
                  .Which.Code.Should().Be(BsonExceptionCode.DynamicPropertiesNotSupported);
        }

        [Fact]
        public void PlainEntity_IsAcceptedByBsonLayer()
        {
            BsonEntityDescription description = AllEntities.Inst.FindBsonEntity(typeof(BsonPlainEntity));

            description.Should().NotBeNull();
            description.EntityType.Should().Be(typeof(BsonPlainEntity));
        }
    }
}
