using AwesomeAssertions;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.DataManagement
{
    public class DynamicPropertiesOwnerTest
    {
        // The bag is exposed read-only; the entity backs it with a private setter that a driver
        // sets via reflection. Client code cannot assign it.
        private sealed class Owner : IDynamicPropertiesOwner
        {
            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        [Fact]
        public void DynamicProperties_DefaultsToNull()
        {
            new Owner().DynamicProperties.Should().BeNull("an unloaded / new entity has no bag");
        }

        [Fact]
        public void DynamicProperties_CanBeSetViaReflection_AsADriverWould()
        {
            var owner = new Owner();

            // this is how a driver populates the bag - by setting the private member reflectively
            typeof(Owner).GetProperty(nameof(Owner.DynamicProperties)).SetValue(owner, new DynamicPropertyBag());

            owner.DynamicProperties.Should().NotBeNull();
            owner.DynamicProperties.Set("k", 5);
            owner.DynamicProperties.Get<int>("k").Should().Be(5);
        }
    }
}
