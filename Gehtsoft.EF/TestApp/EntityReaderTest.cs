using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;
using NUnit.Framework;

namespace TestApp
{
    [TestFixture]
    public class EntityReaderTest
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

        [Test]
        public void TestPropertyReader()
        {
            TestClass v = new TestClass("a1", "a2", "a3", "a4", "a5", "a6");
            Assert.IsNull(EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyR.PropertyR.PropertyR"));
            Assert.AreEqual("a6", EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyR.PropertyR.PropertyV"));
            Assert.AreEqual("a5", EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyR.PropertyV"));
            Assert.AreEqual("a4", EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyV"));
            Assert.AreEqual("a3", EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyV"));
            Assert.AreEqual("a2", EntityPathAccessor.ReadData(v, "PropertyR.PropertyV"));
            Assert.AreEqual("a1", EntityPathAccessor.ReadData(v, "PropertyV"));

            //make sure that caching works
            Assert.AreEqual("a6", EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyR.PropertyR.PropertyV"));
            Assert.AreEqual("a5", EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyR.PropertyV"));

            Assert.AreEqual("r1a6", EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyR.PropertyR.PropertyR1.PropertyX"));
            Assert.AreEqual("r1a5", EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyR.PropertyR1.PropertyX"));
            Assert.AreEqual("r1a4", EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR.PropertyR1.PropertyX"));
            Assert.AreEqual("r1a3", EntityPathAccessor.ReadData(v, "PropertyR.PropertyR.PropertyR1.PropertyX"));
            Assert.AreEqual("r1a2", EntityPathAccessor.ReadData(v, "PropertyR.PropertyR1.PropertyX"));
            Assert.AreEqual("r1a1", EntityPathAccessor.ReadData(v, "PropertyR1.PropertyX"));
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

        [Test]
        public void PKReaderTest()
        {
            Entity1 e1 = new Entity1() { PK = 2 };
            Entity2 e2 = new Entity2() { PK = 5 };

            Assert.IsFalse(this.IsEfEntity());
            Assert.IsTrue(e1.IsEfEntity());
            Assert.IsTrue(e2.IsEfEntity());

            Assert.AreEqual(2, e1.GetEfEntityId<int>());
            Assert.AreEqual(5, e2.GetEfEntityId<int>());
            Assert.AreEqual("5", e2.GetEfEntityId<string>());
        }
    }
}
