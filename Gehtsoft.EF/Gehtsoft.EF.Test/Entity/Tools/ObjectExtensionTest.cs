using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Tools
{
    public class ObjectExtensionTest
    {
        [Entity]
        public class Entity1
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        }

        [Entity]
        public class Entity2
        {
            [PrimaryKey]
            public string Id { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        }

        public class Entity3
        {
            [PrimaryKey]
            public string Id { get; set; }
        }

        [Fact]
        public void TestGetKey()
        {
            var e1 = new Entity1() { Id = 123, Name = "456" };
            e1.GetEfEntityId().Should()
                .NotBeNull()
                .And.BeOfType<int>()
                .And.Be(123);

            e1.GetEfEntityId<int>().Should().Be(123);
            e1.GetEfEntityId<string>().Should().Be("123");

            var e2 = new Entity2 { Id = "789", Name = "name" };
            e2.GetEfEntityId().Should()
                .NotBeNull()
                .And.BeOfType<string>()
                .And.Be("789");

            var e3 = new Entity2 { Id = null, Name = "name" };

            e3.GetEfEntityId().Should().BeNull();

            e3.GetEfEntityId(typeof(int)).Should().Be(0);

            ((Action)(() => ((object)null).GetEfEntityId())).Should().Throw<ArgumentException>();
        }

        [Fact]
        public void IsEfEntity()
        {
            var e1 = new Entity1() { Id = 123, Name = "456" };
            e1.IsEfEntity().Should().BeTrue();

            var e3 = new Entity3();
            e3.IsEfEntity().Should().BeFalse();
        }
    }
}
