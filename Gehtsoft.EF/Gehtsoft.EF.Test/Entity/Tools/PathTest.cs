using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Tools
{
    public class PathTest
    {
        public class CA
        {
            public int CA1 { get; set; } = 123;

            public CB CA2 { get; set; } = new CB();
        }

        public class CB
        {
            public double CB1 { get; set; } = 1.234;
            public CC CB2 { get; set; } = new CC();
        }

        public class CC
        {
            public DateTime CC1 { get; set; } = new DateTime(2015, 5, 22);
            public string CC2 { get; set; } = "teststring";
        }

        [Theory]
        [InlineData("CA1", 123)]
        [InlineData("CA2.CB1", 1.234)]
        [InlineData("CA2.CB2.CC1.Year", 2015)]
        [InlineData("CA2.CB2.CC2", "teststring")]
        [InlineData("CA2.CB2.CC2.Length", 10)]
        public void ReadData(string path, object valueExpected)
        {
            CA ca = new CA();
            EntityPathAccessor.ReadData(ca, path).Should().Be(valueExpected);
        }

        [Fact]
        public void ReadDataFromDynamic()
        {
            dynamic d = new { a = 1, b = new { c = "abcd" } };

            ((object)EntityPathAccessor.ReadData(d, "a")).Should().Be(1);
            ((object)EntityPathAccessor.ReadData(d, "b.c")).Should().Be("abcd");
        }

        [Fact]
        public void ReadDataFromExpando()
        {
            dynamic b = new ExpandoObject();
            ((IDictionary<string, object>)b).Add("c", "abcd");
            dynamic d = new ExpandoObject();
            ((IDictionary<string, object>)d).Add("a", 1);
            ((IDictionary<string, object>)d).Add("b", b);

            ((object)d.a).Should().Be(1);
            ((object)d.b.c).Should().Be("abcd");

            ((object)EntityPathAccessor.ReadData(d, "a")).Should().Be(1);
            ((object)EntityPathAccessor.ReadData(d, "b.c")).Should().Be("abcd");
        }

        [Fact]
        public void Prepare()
        {
            EntityPathAccessor.IsPathCached(typeof(CA), "CA2.CB2.CC1.Day").Should().BeFalse();
            EntityPathAccessor.PreparePath(typeof(CA), "CA2.CB2.CC1.Day");
            EntityPathAccessor.IsPathCached(typeof(CA), "CA2.CB2.CC1.Day").Should().BeTrue();
        }
    }
}
