using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.Entities
{
    public class DynamicPropertiesRecognitionTest
    {
        [Entity(Scope = "dynprops_recognition")]
        public class PlainEntity
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        }

        [Entity(Scope = "dynprops_recognition")]
        [DynamicProperties]
        public class DynPropsDefaultEntity
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        }

        [Entity(Scope = "dynprops_recognition")]
        [DynamicProperties(NameSize = 32, StringValueSize = 1024)]
        public class DynPropsCustomEntity
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        }

        [Fact]
        public void PlainEntity_HasNoDynamicProperties()
        {
            EntityDescriptor descriptor = AllEntities.Get<PlainEntity>();

            descriptor.HasDynamicProperties.Should().BeFalse();
            descriptor.DynamicProperties.Should().BeNull();
        }

        [Fact]
        public void DefaultEntity_IsRecognized_WithDefaultOptions()
        {
            EntityDescriptor descriptor = AllEntities.Get<DynPropsDefaultEntity>();

            descriptor.HasDynamicProperties.Should().BeTrue();
            descriptor.DynamicProperties.Should().NotBeNull();
            descriptor.DynamicProperties.NameSize.Should().Be(64);
            descriptor.DynamicProperties.StringValueSize.Should().Be(256);
        }

        [Fact]
        public void CustomEntity_IsRecognized_WithCustomOptions()
        {
            EntityDescriptor descriptor = AllEntities.Get<DynPropsCustomEntity>();

            descriptor.HasDynamicProperties.Should().BeTrue();
            descriptor.DynamicProperties.Should().NotBeNull();
            descriptor.DynamicProperties.NameSize.Should().Be(32);
            descriptor.DynamicProperties.StringValueSize.Should().Be(1024);
        }

        [Fact]
        public void Attribute_HasExpectedDefaults()
        {
            DynamicPropertiesAttribute attribute = new DynamicPropertiesAttribute();

            attribute.NameSize.Should().Be(64);
            attribute.StringValueSize.Should().Be(256);
            attribute.RealValueSize.Should().Be(-1);
            attribute.RealValuePrecision.Should().Be(-1);
            DynamicPropertiesAttribute.TableSuffix.Should().Be("_props");
        }
    }
}
