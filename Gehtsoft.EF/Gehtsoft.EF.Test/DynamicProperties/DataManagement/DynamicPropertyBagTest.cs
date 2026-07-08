using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.DataManagement
{
    public class DynamicPropertyBagTest
    {
        private static readonly DateTime SampleUtc = new DateTime(2021, 12, 27, 12, 55, 17, DateTimeKind.Utc);

        // ---- supported-type contract ----

        [Theory]
        [InlineData(typeof(bool))]
        [InlineData(typeof(int))]
        [InlineData(typeof(long))]
        [InlineData(typeof(double))]
        [InlineData(typeof(string))]
        [InlineData(typeof(DateTime))]
        [InlineData(typeof(bool?))]
        [InlineData(typeof(int?))]
        [InlineData(typeof(long?))]
        [InlineData(typeof(double?))]
        [InlineData(typeof(DateTime?))]
        public void IsSupportedType_Supported(Type type)
        {
            DynamicPropertyBag.IsSupportedType(type).Should().BeTrue();
        }

        [Theory]
        [InlineData(typeof(short))]
        [InlineData(typeof(byte))]
        [InlineData(typeof(float))]
        [InlineData(typeof(uint))]
        [InlineData(typeof(ulong))]
        [InlineData(typeof(decimal))]
        [InlineData(typeof(decimal?))]
        [InlineData(typeof(Guid))]
        [InlineData(typeof(char))]
        [InlineData(typeof(object))]
        [InlineData(typeof(byte[]))]
        public void IsSupportedType_Unsupported(Type type)
        {
            DynamicPropertyBag.IsSupportedType(type).Should().BeFalse();
        }

        [Fact]
        public void Set_UnsupportedTypes_Throw()
        {
            var bag = new DynamicPropertyBag();
            ((Action)(() => bag.Set("x", (short)5))).Should().Throw<ArgumentException>();
            ((Action)(() => bag.Set("x", 1.5f))).Should().Throw<ArgumentException>();
            ((Action)(() => bag.Set("x", 1.23m))).Should().Throw<ArgumentException>();
            ((Action)(() => bag.Set("x", Guid.NewGuid()))).Should().Throw<ArgumentException>();
            ((Action)(() => bag.Set("x", new object()))).Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Set_NullableWithValue_IsStoredAsUnderlying()
        {
            var bag = new DynamicPropertyBag();
            int? n = 5;
            bag.Set("n", n);
            bag.Get("n").Should().Be(5);
            bag.Get("n").Should().BeOfType<int>();
        }

        // ---- set / get / remove ----

        [Fact]
        public void SetGet_RoundTripsAllSupportedTypes()
        {
            var bag = new DynamicPropertyBag();
            bag.Set("s", "hello");
            bag.Set("i", 123);
            bag.Set("l", 123L);
            bag.Set("r", 4.5);
            bag.Set("b", true);
            bag.Set("d", SampleUtc);

            bag.Count.Should().Be(6);
            bag.Get("s").Should().Be("hello");
            bag.Get("i").Should().Be(123);
            bag.Get("l").Should().Be(123L);
            bag.Get("r").Should().Be(4.5);
            bag.Get("b").Should().Be(true);
            bag.Get("d").Should().Be(SampleUtc);
        }

        [Fact]
        public void GetGeneric_Converts()
        {
            var bag = new DynamicPropertyBag();
            bag.Set("i", 42);
            bag.Set("s", "100");

            bag.Get<long>("i").Should().Be(42L);
            bag.Get<string>("i").Should().Be("42");
            bag.Get<int>("s").Should().Be(100);
        }

        [Fact]
        public void GetGeneric_AbsentKey_ReturnsDefault()
        {
            var bag = new DynamicPropertyBag();
            bag.Get<int>("missing").Should().Be(0);
            bag.Get("missing").Should().BeNull();
        }

        [Fact]
        public void Set_Null_Removes()
        {
            var bag = new DynamicPropertyBag();
            bag.Set("x", "v");
            bag.Contains("x").Should().BeTrue();

            bag.Set("x", null);
            bag.Contains("x").Should().BeFalse();
            bag.Count.Should().Be(0);
        }

        [Fact]
        public void Remove_And_Contains()
        {
            var bag = new DynamicPropertyBag();
            bag.Set("a", 1L);
            bag.Remove("a").Should().BeTrue();
            bag.Remove("a").Should().BeFalse();
            bag.Contains("a").Should().BeFalse();
        }

        [Fact]
        public void Enumeration_YieldsNameValuePairs()
        {
            var bag = new DynamicPropertyBag();
            bag.Set("s", "v");
            bag.Set("i", 5);

            var pairs = bag.OrderBy(p => p.Name).ToList();
            pairs.Should().HaveCount(2);
            pairs[0].Name.Should().Be("i");
            pairs[0].Value.Should().Be(5);
            pairs[1].Name.Should().Be("s");
            pairs[1].Value.Should().Be("v");
        }

        // ---- change tracking ----

        private static IEnumerable<(string, object)> Initial(params (string, object)[] items) => items;

        [Fact]
        public void FreshBag_NotModified()
        {
            new DynamicPropertyBag().AnyModified.Should().BeFalse();
        }

        [Fact]
        public void Initialize_IsUntracked()
        {
            var bag = new DynamicPropertyBag(Initial(("a", 1L), ("b", "x")));

            bag.Count.Should().Be(2);
            bag.AnyModified.Should().BeFalse();
            bag.Added.Should().BeEmpty();
            bag.Changed.Should().BeEmpty();
            bag.Removed.Should().BeEmpty();
        }

        [Fact]
        public void Initialize_RejectsUnsupported()
        {
            ((Action)(() => new DynamicPropertyBag(Initial(("a", 1.23m)))))
                .Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Set_NewName_IsAdded()
        {
            var bag = new DynamicPropertyBag(Initial(("a", 1L)));
            bag.Set("b", 2L);

            bag.Added.Select(p => p.Name).Should().Equal("b");
            bag.Changed.Should().BeEmpty();
            bag.Removed.Should().BeEmpty();
            bag.AnyModified.Should().BeTrue();
        }

        [Fact]
        public void Set_ExistingName_DifferentValue_IsChanged()
        {
            var bag = new DynamicPropertyBag(Initial(("a", 1L)));
            bag.Set("a", 2L);

            bag.Changed.Select(p => p.Name).Should().Equal("a");
            bag.Added.Should().BeEmpty();
            bag.Removed.Should().BeEmpty();
        }

        [Fact]
        public void Set_ExistingName_SameValue_IsNotChanged()
        {
            var bag = new DynamicPropertyBag(Initial(("a", 1L)));
            bag.Set("a", 1L);

            bag.AnyModified.Should().BeFalse();
        }

        [Fact]
        public void Remove_BaselineName_IsRemoved()
        {
            var bag = new DynamicPropertyBag(Initial(("a", 1L)));
            bag.Remove("a");

            bag.Removed.Should().Equal("a");
            bag.Added.Should().BeEmpty();
            bag.Changed.Should().BeEmpty();
        }

        [Fact]
        public void AddThenRemove_NetsToNothing()
        {
            var bag = new DynamicPropertyBag(Initial(("a", 1L)));
            bag.Set("b", 2L);
            bag.Remove("b");

            bag.AnyModified.Should().BeFalse();
        }

        [Fact]
        public void AcceptChanges_ClearsTracking()
        {
            var bag = new DynamicPropertyBag(Initial(("a", 1L)));
            bag.Set("b", 2L);
            bag.Set("a", 9L);
            bag.AnyModified.Should().BeTrue();

            bag.AcceptChanges();

            bag.AnyModified.Should().BeFalse();
            bag.Get("b").Should().Be(2L);
            bag.Get("a").Should().Be(9L);
        }
    }
}
