using System;
using FluentAssertions;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using Xunit;

namespace Gehtsoft.EF.Test.Entity
{
    public class EntityDescriptorTest
    {
        private readonly EntityFinder.EntityTypeInfo[] mEntities = EntityFinder.FindEntities(new[] { typeof(Order).Assembly }, "northwind", false);

        [Fact]
        public void Compare_ToNull()
        {
            mEntities[0].CompareTo(null).Should().BeGreaterThan(0);
        }

        [Fact]
        public void Compare_ADependsOnB()
        {
            var a = Array.Find(mEntities, e => e.EntityType == typeof(Product));
            var b = Array.Find(mEntities, e => e.EntityType == typeof(Category));

            a.CompareTo(b).Should().BeGreaterThan(0);
        }

        [Fact]
        public void Compare_BDependsOnA()
        {
            var a = Array.Find(mEntities, e => e.EntityType == typeof(Product));
            var b = Array.Find(mEntities, e => e.EntityType == typeof(Category));

            b.CompareTo(a).Should().BeLessThan(0);
        }

        [Fact]
        public void Compare_DependsIndirectly()
        {
            var a = Array.Find(mEntities, e => e.EntityType == typeof(OrderDetail));
            var b = Array.Find(mEntities, e => e.EntityType == typeof(Category));

            a.CompareTo(b).Should().BeGreaterThan(0);
        }

        [Fact]
        public void Compare_Independent()
        {
            var a = Array.Find(mEntities, e => e.EntityType == typeof(Category));
            var b = Array.Find(mEntities, e => e.EntityType == typeof(Territory));

            a.CompareTo(b).Should().BeLessThan(0);
        }
    }
}
