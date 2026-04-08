using System;
using System.Collections;
using System.Collections.Generic;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;
using AwesomeAssertions;
using Xunit;

namespace Gehtsoft.EF.Test.Legacy
{
    public class EntityReaderTests
    {
        public class TestClass1
        {
            public string PropertyX { get; set; }
        }

        public class TestClass
        {
            public string PropertyV { get; set; }
            public TestClass PropertyR { get; set; }
            public TestClass1 PropertyR1 { get; set; }

            private TestClass(IEnumerator<string> en)
            {
                PropertyV = en.Current;
                PropertyR1 = new TestClass1() { PropertyX = "r1" + en.Current };
                if (en.MoveNext())
                    PropertyR = new TestClass(en);
            }

            public TestClass(params string[] args)
            {
                if (args == null || args.Length == 0)
                {
                    PropertyV = null;
                    PropertyR = null;
                    return;
                }

                using (IEnumerator<string> en = ((IEnumerable<string>)args).GetEnumerator())
                {
                    en.Reset();
                    en.MoveNext();
                    PropertyV = en.Current;
                    PropertyR1 = new TestClass1() { PropertyX = "r1" + en.Current };
                    if (en.MoveNext())
                        PropertyR = new TestClass(en);
                }
            }
        }

        [Fact]
        public void TestPropertyReader()
        {
            TestClass v = new TestClass("a1", "a2", "a3", "a4", "a5", "a6");
            EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyR.PropertyR.PropertyR").Should().BeNull();
            EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyR.PropertyR.PropertyV").Should().Be("a6");
            EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyR.PropertyV").Should().Be("a5");
            EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyV").Should().Be("a4");
            EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyV").Should().Be("a3");
            EntityPathAccessor.ReadData(v, "PropertyR.PropertyV").Should().Be("a2");
            EntityPathAccessor.ReadData(v, "PropertyV").Should().Be("a1");

            //make sure that caching works
            EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyR.PropertyR.PropertyV").Should().Be("a6");
            EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyR.PropertyV").Should().Be("a5");

            EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyR.PropertyR.PropertyR1.PropertyX").Should().Be("r1a6");
            EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyR.PropertyR1.PropertyX").Should().Be("r1a5");
            EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyR1.PropertyX").Should().Be("r1a4");
            EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR1.PropertyX").Should().Be("r1a3");
            EntityPathAccessor.ReadData(v, "PropertyR.PropertyR1.PropertyX").Should().Be("r1a2");
            EntityPathAccessor.ReadData(v, "PropertyR1.PropertyX").Should().Be("r1a1");
        }

        [Entity]
        public class Entity1
        {
            [AutoId]
            public int PK { get; set; }
        }

        [Entity]
        public class Entity2
        {
            [EntityProperty(PrimaryKey = true)]
            public int PK { get; set; }
        }

        [Fact]
        public void PKReaderTest()
        {
            Entity1 e1 = new Entity1() { PK = 2 };
            Entity2 e2 = new Entity2() { PK = 5 };

            this.IsEfEntity().Should().BeFalse();
            e1.IsEfEntity().Should().BeTrue();
            e2.IsEfEntity().Should().BeTrue();

            e1.GetEfEntityId<int>().Should().Be(2);
            e2.GetEfEntityId<int>().Should().Be(5);
            e2.GetEfEntityId<string>().Should().Be("5");
        }
    }
}
