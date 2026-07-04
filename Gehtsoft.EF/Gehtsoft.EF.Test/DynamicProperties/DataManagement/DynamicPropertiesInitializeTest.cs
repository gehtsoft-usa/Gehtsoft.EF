using System;
using AwesomeAssertions;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.DataManagement
{
    public class DynamicPropertiesInitializeTest
    {
        private sealed class Owner : IDynamicPropertiesOwner
        {
            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        private sealed class NotAnOwner
        {
        }

        [Fact]
        public void InitializeDynamicProperties_AttachesEmptyNewBag()
        {
            var owner = new Owner();

            DynamicPropertyBag bag = owner.InitializeDynamicProperties();

            bag.Should().NotBeNull();
            owner.DynamicProperties.Should().BeSameAs(bag);
            bag.Count.Should().Be(0);
            bag.IsNew.Should().BeTrue();
        }

        [Fact]
        public void InitializeDynamicProperties_OnNonOwner_Throws()
        {
            ((Action)(() => new NotAnOwner().InitializeDynamicProperties()))
                .Should().Throw<ArgumentException>();
        }

        [Fact]
        public void FreshBag_IsNotNew()
        {
            new DynamicPropertyBag().IsNew.Should().BeFalse();
        }

        [Fact]
        public void InitializedBag_IsNotNew()
        {
            new DynamicPropertyBag(new[] { ("a", (object)1L) }).IsNew.Should().BeFalse();
        }

        [Fact]
        public void AcceptChanges_ClearsIsNew()
        {
            var owner = new Owner();
            DynamicPropertyBag bag = owner.InitializeDynamicProperties();
            bag.Set("k", 5);
            bag.IsNew.Should().BeTrue();

            bag.AcceptChanges();

            bag.IsNew.Should().BeFalse();
        }
    }
}
