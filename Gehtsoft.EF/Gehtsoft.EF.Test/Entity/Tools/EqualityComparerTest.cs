using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Tools
{
    public class EqualityComparerTest
    {
        [Entity]
        public class Dictionary
        {
            [AutoId]
            public int Key { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        }

        [Entity]
        public class Data
        {
            [AutoId]
            public int Key { get; set; }

            [ForeignKey]
            public Dictionary Dictionary { get; set; }

            [EntityProperty(DbType = DbType.Date)]
            public DateTime Date { get; set; }

            [EntityProperty(DbType = DbType.DateTime)]
            public DateTime Stamp { get; set; }

            [EntityProperty(DbType = DbType.Double, Precision = 5)]
            public double Number { get; set; }

            [EntityProperty]
            public int Integer { get; set; }

            [EntityProperty]
            public bool Flag { get; set; }

            [EntityProperty]
            public int? Nullable { get; set; }

            public int NotToBeConsidered { get; set; }
        }

        private Data Create(int dataKey = 1, int dictionaryKey = 2, string dictionaryName = "dictname",
                            DateTime? date = null,
                            DateTime? stamp = null,
                            double number = 1.234, int integer = 1234, bool flag = true, int? nullable = 123)
        {
            DateTime _date = date ?? new DateTime(2015, 1, 2, 1, 2, 3);
            DateTime _stamp = stamp ?? new DateTime(2016, 2, 3, 22, 33, 44);

            return new Data
            {
                Key = dataKey,
                Dictionary = new Dictionary()
                {
                    Key = dictionaryKey,
                    Name = dictionaryName
                },
                Date = _date,
                Stamp = _stamp,
                Number = number,
                Integer = integer,
                Nullable = nullable,
            };
        }

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(false, false, true)]
        public void CompareNullOrSame(bool firstNull, bool secondNull, bool expected)
        {
            var d = Create();

            var f = firstNull ? null : d;
            var s = secondNull ? null : d;

            EntityComparerHelper.Equals(f, s).Should().Be(expected);

            var c = new EntityEqualityComparer<Data>();
            c.Equals(f, s).Should().Be(expected);

        }

        [Fact]
        public void ComparseSimilar()
        {
            var f = Create();
            var s = Create();

            EntityComparerHelper.Equals(f, s).Should().BeTrue();
        }

        [Fact]
        public void CompareIgnoresNonProperties()
        {
            var f = Create();
            var s = Create();

            f.NotToBeConsidered = 1234;
            s.NotToBeConsidered = 2345;

            EntityComparerHelper.Equals(f, s).Should().BeTrue();
        }

        [Fact]
        public void CompareDifferent()
        {
            var f = Create();

            var s = Create();
            s.Key += 1;
            EntityComparerHelper.Equals(f, s).Should().BeFalse();

            s = Create();
            s.Dictionary.Key += 1;
            EntityComparerHelper.Equals(f, s).Should().BeFalse();

            s = Create();
            s.Dictionary.Name += "a";
            EntityComparerHelper.Equals(f, s).Should().BeFalse();

            s = Create();
            s.Number += 1;
            EntityComparerHelper.Equals(f, s).Should().BeFalse();

            s = Create();
            s.Integer += 1;
            EntityComparerHelper.Equals(f, s).Should().BeFalse();

            s = Create();
            s.Flag = !s.Flag;
            EntityComparerHelper.Equals(f, s).Should().BeFalse();

            s = Create();
            s.Number += 1;
            EntityComparerHelper.Equals(f, s).Should().BeFalse();
        }

        [Fact]
        public void HashSimilar()
        {
            var f = Create();
            var s = Create();

            (EntityComparerHelper.GetHashCode(f) == EntityComparerHelper.GetHashCode(s))
                .Should().BeTrue();

            var c = new EntityEqualityComparer<Data>();
            c.GetHashCode(f).Should().Be(EntityComparerHelper.GetHashCode(f));
        }

        [Fact]
        public void HashDifferent()
        {
            var f = Create();

            var s = Create();
            s.Key += 1;
            (EntityComparerHelper.GetHashCode(f) == EntityComparerHelper.GetHashCode(s))
               .Should().BeFalse();

            s = Create();
            s.Dictionary.Key += 1;
            (EntityComparerHelper.GetHashCode(f) == EntityComparerHelper.GetHashCode(s))
               .Should().BeFalse();

            s = Create();
            s.Dictionary.Name += "a";
            (EntityComparerHelper.GetHashCode(f) == EntityComparerHelper.GetHashCode(s))
               .Should().BeFalse();

            s = Create();
            s.Number += 1;
            (EntityComparerHelper.GetHashCode(f) == EntityComparerHelper.GetHashCode(s))
               .Should().BeFalse();

            s = Create();
            s.Integer += 1;
            (EntityComparerHelper.GetHashCode(f) == EntityComparerHelper.GetHashCode(s))
               .Should().BeFalse();

            s = Create();
            s.Flag = !s.Flag;
            (EntityComparerHelper.GetHashCode(f) == EntityComparerHelper.GetHashCode(s))
               .Should().BeFalse();

            s = Create();
            s.Number += 1;
            (EntityComparerHelper.GetHashCode(f) == EntityComparerHelper.GetHashCode(s))
               .Should().BeFalse();
        }

        [Fact]
        public void HashIgnoresNonProperties()
        {
            var f = Create();
            var s = Create();

            f.NotToBeConsidered = 1234;
            s.NotToBeConsidered = 2345;

            (EntityComparerHelper.GetHashCode(f) == EntityComparerHelper.GetHashCode(s))
               .Should().BeTrue();
        }

        private void AddCode(Dictionary<int, int> codes, int code)
        {
            if (codes.TryGetValue(code, out var count))
                codes[code] = count + 1;
            else
                codes[code] = 1;
        }

        [Fact]
        public void TestDistribution()
        {
            var codes = new Dictionary<int, int>();

            var e = Create();
            for (int i = 0; i < 100; i++)
            {
                e.Key += i * 10;
                AddCode(codes, EntityComparerHelper.GetHashCode(e));
            }

            for (int i = 0; i < 100; i++)
            {
                e.Date = e.Date.AddDays(i * 5);
                AddCode(codes, EntityComparerHelper.GetHashCode(e));
            }

            for (int i = 0; i < 100; i++)
            {
                e.Stamp = e.Stamp.AddMinutes(i * 5);
                AddCode(codes, EntityComparerHelper.GetHashCode(e));
            }

            codes.Count.Should().BeGreaterThan(200);
            codes.Values.Any(v => v > 10).Should().BeFalse();
        }

        [Fact]
        public void ConsiderPrecisionForNumbers()
        {
            var f = Create();
            var s = Create();
            s.Number += 1e-6;
            EntityComparerHelper.Equals(f, s).Should().BeTrue();
        }

        [Fact]
        public void IgnoreTimeForDates()
        {
            var f = Create();
            var s = Create();
            s.Date = s.Date.AddMinutes(1);
            EntityComparerHelper.Equals(f, s).Should().BeTrue();
        }
    }
}
