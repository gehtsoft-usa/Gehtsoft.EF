using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using NUnit.Framework;

namespace TestApp
{
    [TestFixture]
    public class EntityNameConvertorTest
    {
        [TestCase(null, EntityNamingPolicy.AsIs, null)]
        [TestCase("", EntityNamingPolicy.AsIs, "")]
        
        [TestCase("abc", EntityNamingPolicy.AsIs, "abc")]
        [TestCase("ABC", EntityNamingPolicy.AsIs, "ABC")]
        [TestCase("MixedCase", EntityNamingPolicy.AsIs, "MixedCase")]
        [TestCase("mIXEDcASE", EntityNamingPolicy.AsIs, "mIXEDcASE")]
        [TestCase("mixedCase", EntityNamingPolicy.AsIs, "mixedCase")]
        [TestCase("a", EntityNamingPolicy.AsIs, "a")]
        
        [TestCase("abc", null, "abc")]
        [TestCase("ABC", null, "ABC")]
        [TestCase("MixedCase", null, "MixedCase")]
        [TestCase("mIXEDcASE", null, "mIXEDcASE")]
        [TestCase("mixedCase", null, "mixedCase")]
        [TestCase("a", null, "a")]


        [TestCase("abc", EntityNamingPolicy.LowerCase, "abc")]
        [TestCase("ABC", EntityNamingPolicy.LowerCase, "abc")]
        [TestCase("MixedCase", EntityNamingPolicy.LowerCase, "mixedcase")]
        [TestCase("mIXEDcASE", EntityNamingPolicy.LowerCase, "mixedcase")]
        [TestCase("mixedCase", EntityNamingPolicy.LowerCase, "mixedcase")]
        [TestCase("A", EntityNamingPolicy.LowerCase, "a")]

        [TestCase("abc", EntityNamingPolicy.UpperCase, "ABC")]
        [TestCase("ABC", EntityNamingPolicy.UpperCase, "ABC")]
        [TestCase("MixedCase", EntityNamingPolicy.UpperCase, "MIXEDCASE")]
        [TestCase("mIXEDcASE", EntityNamingPolicy.UpperCase, "MIXEDCASE")]
        [TestCase("mixedCase", EntityNamingPolicy.UpperCase, "MIXEDCASE")]
        [TestCase("a", EntityNamingPolicy.UpperCase, "A")]

        [TestCase("abc", EntityNamingPolicy.LowerFirstCharacter, "abc")]
        [TestCase("ABC", EntityNamingPolicy.LowerFirstCharacter, "aBC")]
        [TestCase("MixedCase", EntityNamingPolicy.LowerFirstCharacter, "mixedCase")]
        [TestCase("MIXEDcASE", EntityNamingPolicy.LowerFirstCharacter, "mIXEDcASE")]
        [TestCase("mixedCase", EntityNamingPolicy.LowerFirstCharacter, "mixedCase")]
        [TestCase("A", EntityNamingPolicy.LowerFirstCharacter, "a")]

        [TestCase(null, EntityNamingPolicy.UpperFirstCharacter, null)]
        [TestCase("", EntityNamingPolicy.UpperFirstCharacter, "")]
        [TestCase("abc", EntityNamingPolicy.UpperFirstCharacter, "Abc")]
        [TestCase("ABC", EntityNamingPolicy.UpperFirstCharacter, "ABC")]
        [TestCase("MixedCase", EntityNamingPolicy.UpperFirstCharacter, "MixedCase")]
        [TestCase("mIXEDcASE", EntityNamingPolicy.UpperFirstCharacter, "MIXEDcASE")]
        [TestCase("mixedCase", EntityNamingPolicy.UpperFirstCharacter, "MixedCase")]
        [TestCase("a", EntityNamingPolicy.UpperFirstCharacter, "A")]

        [TestCase("abc", EntityNamingPolicy.LowerCaseWithUnderscores, "abc")]
        [TestCase("ABC", EntityNamingPolicy.LowerCaseWithUnderscores, "abc")]
        [TestCase("MixedCase", EntityNamingPolicy.LowerCaseWithUnderscores, "mixed_case")]
        [TestCase("MIXEDcASE", EntityNamingPolicy.LowerCaseWithUnderscores, "mixedc_ase")]
        [TestCase("mixedCase", EntityNamingPolicy.LowerCaseWithUnderscores, "mixed_case")]
        [TestCase("A", EntityNamingPolicy.LowerCaseWithUnderscores, "a")]

        [TestCase("abc", EntityNamingPolicy.UpperCaseWithUnderscopes, "ABC")]
        [TestCase("ABC", EntityNamingPolicy.UpperCaseWithUnderscopes, "ABC")]
        [TestCase("MixedCase", EntityNamingPolicy.UpperCaseWithUnderscopes, "MIXED_CASE")]
        [TestCase("MIXEDcASE", EntityNamingPolicy.UpperCaseWithUnderscopes, "MIXEDC_ASE")]
        [TestCase("mixedCase", EntityNamingPolicy.UpperCaseWithUnderscopes, "MIXED_CASE")]
        [TestCase("a", EntityNamingPolicy.UpperCaseWithUnderscopes, "A")]

        public void TestNameConvertor(string originalName, EntityNamingPolicy? policy, string expectedResult) => Assert.AreEqual(expectedResult, EntityNameConvertor.ConvertName(originalName, policy));

        [TestCase("a", EntityNamingPolicy.AsIs, "a")]
        [TestCase("a", EntityNamingPolicy.UpperCase, "A")]
        [TestCase("entity", EntityNamingPolicy.AsIs, "entity")]
        [TestCase("entity", EntityNamingPolicy.UpperCase, "ENTITIES")]
        [TestCase("box", EntityNamingPolicy.UpperCase, "BOXES")]
        [TestCase("crack", EntityNamingPolicy.UpperCase, "CRACKS")]
        [TestCase("igloo", EntityNamingPolicy.LowerCaseWithUnderscores, "igloos")]
        [TestCase("RottenPotato", EntityNamingPolicy.LowerCaseWithUnderscores, "rotten_potatoes")]
        public void TestTableNameConvertor(string originalName, EntityNamingPolicy? policy, string expectedResult) => Assert.AreEqual(expectedResult, EntityNameConvertor.ConvertTableName(originalName, policy));

        [Entity(Scope = "naming", NamingPolicy = EntityNamingPolicy.BackwardCompatibility)]
        public class TestEntity2
        {
            [EntityProperty(AutoId = true)]
            public int Identifier { get; set; }
        }

        [Entity(Scope = "naming", Table = "specialTestName")]
        public class TestEntity3
        {
            [EntityProperty(AutoId = true)]
            public int Identifier { get; set; }
        }


        [Entity(Scope = "naming")]
        public class TestEntity1
        {
            [EntityProperty(AutoId = true)]
            public int Identifier { get; set; }

            [EntityProperty(Size = 32)]
            public string TestField { get; set; }

            [EntityProperty(ForeignKey = true)]
            public TestEntity2 Reference { get; set; }
        }


        [Test]
        public void TestEntityNaming()
        {
            Assert.AreEqual(EntityNamingPolicy.BackwardCompatibility, AllEntities.Inst.NamingPolicy.Default);
            Assert.AreEqual(EntityNamingPolicy.BackwardCompatibility, AllEntities.Inst.NamingPolicy["naming"]);
            EntityDescriptor descriptor1 = AllEntities.Inst[typeof(TestEntity1)];
            EntityDescriptor descriptor2 = AllEntities.Inst[typeof(TestEntity2)];
            EntityDescriptor descriptor3 = AllEntities.Inst[typeof(TestEntity3)];
            
            Assert.AreEqual("TestEntity1", descriptor1.TableDescriptor.Name);
            Assert.AreEqual("identifier", descriptor1.TableDescriptor[0].Name);
            Assert.AreEqual("testfield", descriptor1.TableDescriptor[1].Name);
            Assert.AreEqual("reference", descriptor1.TableDescriptor[2].Name);

            Assert.AreEqual("TestEntity2", descriptor2.TableDescriptor.Name);
            Assert.AreEqual("identifier", descriptor2.TableDescriptor[0].Name);

            AllEntities.Inst.ForgetScope("naming");
            AllEntities.Inst.NamingPolicy["naming"] = EntityNamingPolicy.AsIs;
            descriptor1 = AllEntities.Inst[typeof(TestEntity1)];
            descriptor2 = AllEntities.Inst[typeof(TestEntity2)];
            Assert.AreEqual("TestEntity1", descriptor1.TableDescriptor.Name);
            Assert.AreEqual("Id", descriptor1.TableDescriptor[0].Name);
            Assert.AreEqual("TestField", descriptor1.TableDescriptor[1].Name);
            Assert.AreEqual("TestEntity2Ref", descriptor1.TableDescriptor[2].Name);
            Assert.AreEqual("TestEntity2", descriptor2.TableDescriptor.Name);
            Assert.AreEqual("identifier", descriptor2.TableDescriptor[0].Name);

            AllEntities.Inst.ForgetScope("naming");
            AllEntities.Inst.NamingPolicy["naming"] = EntityNamingPolicy.LowerCaseWithUnderscores;
            descriptor1 = AllEntities.Inst[typeof(TestEntity1)];
            descriptor2 = AllEntities.Inst[typeof(TestEntity2)];
            descriptor3 = AllEntities.Inst[typeof(TestEntity3)];
            Assert.AreEqual("test_entity1s", descriptor1.TableDescriptor.Name);
            Assert.AreEqual("specialTestName", descriptor3.TableDescriptor.Name);
            Assert.AreEqual("id", descriptor1.TableDescriptor[0].Name);
            Assert.AreEqual("test_field", descriptor1.TableDescriptor[1].Name);
            Assert.AreEqual("test_entity2_ref", descriptor1.TableDescriptor[2].Name);
            Assert.AreEqual("TestEntity2", descriptor2.TableDescriptor.Name);
            Assert.AreEqual("identifier", descriptor2.TableDescriptor[0].Name);
        }

        [Entity(Scope = "naming2")]
        public class TestObject
        {
            [AutoId]
            public int ID { get; set; }
        }

        [Entity(Scope = "naming2")]
        public class TestEntity
        {
            [AutoId]
            public int ID { get; set; }

            [ForeignKey]
            public TestObject Reference { get; set; }
        }
        
        [Entity(Scope = "naming2", Table = "men")]
        public class Man
        {
            [AutoId]
            public int ID { get; set; }

        }

        [Test]
        public void TestInQueries()
        {
            using (SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory())
            {
                AllEntities.Inst.NamingPolicy["naming2"] = EntityNamingPolicy.LowerCaseWithUnderscores;
                CreateEntityController controller = new CreateEntityController(typeof(TestObject), "naming2");
                controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update);
                TableDescriptor[] s = connection.Schema();

                Assert.IsTrue(s.Any(d => d.Name == "test_objects"));
                Assert.IsTrue(s.Any(d => d.Name == "test_entities"));
                Assert.IsTrue(s.Any(d => d.Name == "men"));

                controller = new CreateEntityController(typeof(TestObject), "naming2");
                controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update);


            }
        }
    }
}
