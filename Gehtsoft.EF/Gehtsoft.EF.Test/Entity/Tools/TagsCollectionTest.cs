using FluentAssertions;
using Gehtsoft.EF.Test.Utils;
using Gehtsoft.EF.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Tools
{
    public class TagsCollectionTest
    {
        [Fact]
        public void ReturnDefaultIfNoValue()
        {
            var tags = new TagCollection();
            tags.GetTag("mytag").Should().BeNull();
            tags.GetTag<int>("mytag").Should().Be(0);
            tags.GetTag<int>("mytag", 123).Should().Be(123);

            tags.SetTag("othertag", 456);
            tags.GetTag("mytag").Should().BeNull();
            tags.GetTag<int>("mytag").Should().Be(0);
            tags.GetTag<int>("mytag", 123).Should().Be(123);
        }

        [Fact]
        public void ReturnValue()
        {
            var tags = new TagCollection();
            tags.SetTag("1", "one");
            tags.SetTag("2", "two");
            tags.SetTag(2, "another two");
            tags.SetTag(typeof(string), "string");
            tags.SetTag(typeof(int), "int");

            tags.GetTag("1").Should().Be("one");
            tags.GetTag("2").Should().Be("two");
            tags.GetTag(2).Should().Be("another two");
            tags.GetTag(typeof(string)).Should().Be("string");
            tags.GetTag(typeof(int)).Should().Be("int");

            tags.GetTag<string>("1").Should().Be("one");
        }

        [Fact]
        public void AssignNullRemoves()
        {
            var tags = new TagCollection();
            tags.SetTag("1", "one");
            tags.SetTag("2", "two");
            tags.SetTag(2, "another two");
            tags.SetTag(typeof(string), "string");
            tags.SetTag(typeof(int), "int");

            tags.SetTag(2, null);

            tags.Keys.Should()
                .HaveCount(4)
                .And.NotContain(2);
        }

        [Fact]
        public void Remove()
        {
            var tags = new TagCollection();
            tags.SetTag("1", "one");
            tags.SetTag("2", "two");
            tags.SetTag(2, "another two");
            tags.SetTag(typeof(string), "string");
            tags.SetTag(typeof(int), "int");

            tags.Remove("2");

            tags.Keys.Should()
                .HaveCount(4)
                .And.NotContain("2");
        }

        [Fact]
        public void ConvertType()
        {
            var tags = new TagCollection();
            tags.SetTag("one", "1");
            tags.GetTag("one", typeof(int))
                .Should()
                .BeOfType<int>()
                .And.Be(1);
        }

        [Fact]
        public void Keys_HasElements()
        {
            var tags = new TagCollection();
            tags.SetTag("1", "one");
            tags.SetTag("2", "two");
            tags.SetTag(2, "another two");
            tags.SetTag(typeof(string), "string");
            tags.SetTag(typeof(int), "int");

            tags.Keys.Should()
                .HaveCount(5)
                .And.Contain("1")
                .And.Contain("2")
                .And.Contain(2)
                .And.Contain(typeof(string))
                .And.Contain(typeof(int));
        }

        [Fact]
        public void Enumerator_HasElements()
        {
            var tags = new TagCollection();
            tags.SetTag("1", "one");
            tags.SetTag("2", "two");

            tags.Should()
                .HaveCount(2)
                .And.Contain(e => e.Key.Equals("1") && e.Value.Equals("one"))
                .And.Contain(e => e.Key.Equals("2") && e.Value.Equals("two"));
        }

        [Fact]
        public void Keys_NoElements()
        {
            var tags = new TagCollection();
            tags.Keys.Should().BeEmpty();
        }

        [Fact]
        public void Enumerator_NoElements()
        {
            var tags = new TagCollection();
            tags.Should().BeEmpty();
        }
    }
}

