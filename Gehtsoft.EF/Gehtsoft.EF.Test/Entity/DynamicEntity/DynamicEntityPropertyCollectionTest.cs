using System;
using System.Collections;
using System.Collections.Generic;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.DynamicEntityCollectionTests
{
    /// <summary>
    /// The dynamic-entity property collection is an <see cref="IList{T}"/> that is mutable when built empty
    /// and immutable when built from a source sequence (every mutator then throws). These tests cover both
    /// modes and every list operation, including the reference-identity <c>IndexOf</c>.
    /// </summary>
    public class DynamicEntityPropertyCollectionTest
    {
        private static IDynamicEntityProperty Prop(string name) => new DynamicEntityProperty(typeof(int), name, null);

        [Fact]
        public void Mutable_SupportsFullListSurface()
        {
            var c = new DynamicEntityPropertyCollection();
            c.IsReadOnly.Should().BeFalse();

            IDynamicEntityProperty a = Prop("a"), b = Prop("b"), d = Prop("d");
            c.Add(a);
            c.Add(b);
            c.Count.Should().Be(2);

            c.IndexOf(a).Should().Be(0);
            c.Contains(b).Should().BeTrue();
            c.Contains(d).Should().BeFalse();

            c.Insert(1, d);
            c.IndexOf(d).Should().Be(1);
            c[1].Should().BeSameAs(d);

            c[1] = Prop("d2");
            c.IndexOf(d).Should().Be(-1);

            c.Remove(a).Should().BeTrue();
            c.Remove(a).Should().BeFalse(); // already gone

            var arr = new IDynamicEntityProperty[c.Count];
            c.CopyTo(arr, 0);
            arr.Should().HaveCount(c.Count);

            var names = new List<string>();
            foreach (IDynamicEntityProperty p in c)
                names.Add(p.Name);
            names.Should().NotBeEmpty();

            IEnumerator untyped = ((IEnumerable)c).GetEnumerator();
            untyped.MoveNext().Should().BeTrue();

            c.RemoveAt(0);
            c.Clear();
            c.Count.Should().Be(0);
        }

        [Fact]
        public void ReadOnly_ThrowsOnEveryMutation()
        {
            var c = new DynamicEntityPropertyCollection(new[] { Prop("a"), Prop("b") });
            c.IsReadOnly.Should().BeTrue();
            c.Count.Should().Be(2);
            c[0].Name.Should().Be("a"); // getter is allowed

            ((Action)(() => c.Add(Prop("c")))).Should().Throw<InvalidOperationException>();
            ((Action)(() => c.Clear())).Should().Throw<InvalidOperationException>();
            ((Action)(() => c.Remove(c[0]))).Should().Throw<InvalidOperationException>();
            ((Action)(() => c.Insert(0, Prop("x")))).Should().Throw<InvalidOperationException>();
            ((Action)(() => c.RemoveAt(0))).Should().Throw<InvalidOperationException>();
            ((Action)(() => c[0] = Prop("y"))).Should().Throw<InvalidOperationException>();
        }
    }
}
