using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Naming
{
    public class NameConvertorTest
    {
        [Theory]
        [InlineData(null, EntityNamingPolicy.AsIs, null)]
        [InlineData("", EntityNamingPolicy.AsIs, "")]

        [InlineData("abc", EntityNamingPolicy.AsIs, "abc")]
        [InlineData("ABC", EntityNamingPolicy.AsIs, "ABC")]
        [InlineData("MixedCase", EntityNamingPolicy.AsIs, "MixedCase")]
        [InlineData("mIXEDcASE", EntityNamingPolicy.AsIs, "mIXEDcASE")]
        [InlineData("mixedCase", EntityNamingPolicy.AsIs, "mixedCase")]
        [InlineData("a", EntityNamingPolicy.AsIs, "a")]

        [InlineData("abc", null, "abc")]
        [InlineData("ABC", null, "ABC")]
        [InlineData("MixedCase", null, "MixedCase")]
        [InlineData("mIXEDcASE", null, "mIXEDcASE")]
        [InlineData("mixedCase", null, "mixedCase")]
        [InlineData("a", null, "a")]

        [InlineData("abc", EntityNamingPolicy.LowerCase, "abc")]
        [InlineData("ABC", EntityNamingPolicy.LowerCase, "abc")]
        [InlineData("MixedCase", EntityNamingPolicy.LowerCase, "mixedcase")]
        [InlineData("mIXEDcASE", EntityNamingPolicy.LowerCase, "mixedcase")]
        [InlineData("mixedCase", EntityNamingPolicy.LowerCase, "mixedcase")]
        [InlineData("A", EntityNamingPolicy.LowerCase, "a")]

        [InlineData("abc", EntityNamingPolicy.UpperCase, "ABC")]
        [InlineData("ABC", EntityNamingPolicy.UpperCase, "ABC")]
        [InlineData("MixedCase", EntityNamingPolicy.UpperCase, "MIXEDCASE")]
        [InlineData("mIXEDcASE", EntityNamingPolicy.UpperCase, "MIXEDCASE")]
        [InlineData("mixedCase", EntityNamingPolicy.UpperCase, "MIXEDCASE")]
        [InlineData("a", EntityNamingPolicy.UpperCase, "A")]

        [InlineData("abc", EntityNamingPolicy.LowerFirstCharacter, "abc")]
        [InlineData("ABC", EntityNamingPolicy.LowerFirstCharacter, "aBC")]
        [InlineData("MixedCase", EntityNamingPolicy.LowerFirstCharacter, "mixedCase")]
        [InlineData("MIXEDcASE", EntityNamingPolicy.LowerFirstCharacter, "mIXEDcASE")]
        [InlineData("mixedCase", EntityNamingPolicy.LowerFirstCharacter, "mixedCase")]
        [InlineData("A", EntityNamingPolicy.LowerFirstCharacter, "a")]

        [InlineData(null, EntityNamingPolicy.UpperFirstCharacter, null)]
        [InlineData("", EntityNamingPolicy.UpperFirstCharacter, "")]
        [InlineData("abc", EntityNamingPolicy.UpperFirstCharacter, "Abc")]
        [InlineData("ABC", EntityNamingPolicy.UpperFirstCharacter, "ABC")]
        [InlineData("MixedCase", EntityNamingPolicy.UpperFirstCharacter, "MixedCase")]
        [InlineData("mIXEDcASE", EntityNamingPolicy.UpperFirstCharacter, "MIXEDcASE")]
        [InlineData("mixedCase", EntityNamingPolicy.UpperFirstCharacter, "MixedCase")]
        [InlineData("a", EntityNamingPolicy.UpperFirstCharacter, "A")]

        [InlineData("abc", EntityNamingPolicy.LowerCaseWithUnderscores, "abc")]
        [InlineData("ABC", EntityNamingPolicy.LowerCaseWithUnderscores, "abc")]
        [InlineData("MixedCase", EntityNamingPolicy.LowerCaseWithUnderscores, "mixed_case")]
        [InlineData("MIXEDcASE", EntityNamingPolicy.LowerCaseWithUnderscores, "mixedc_ase")]
        [InlineData("mixedCase", EntityNamingPolicy.LowerCaseWithUnderscores, "mixed_case")]
        [InlineData("A", EntityNamingPolicy.LowerCaseWithUnderscores, "a")]

        [InlineData("abc", EntityNamingPolicy.UpperCaseWithUnderscopes, "ABC")]
        [InlineData("ABC", EntityNamingPolicy.UpperCaseWithUnderscopes, "ABC")]
        [InlineData("MixedCase", EntityNamingPolicy.UpperCaseWithUnderscopes, "MIXED_CASE")]
        [InlineData("MIXEDcASE", EntityNamingPolicy.UpperCaseWithUnderscopes, "MIXEDC_ASE")]
        [InlineData("mixedCase", EntityNamingPolicy.UpperCaseWithUnderscopes, "MIXED_CASE")]
        [InlineData("a", EntityNamingPolicy.UpperCaseWithUnderscopes, "A")]

        public void ConvertName(string originalName, EntityNamingPolicy? policy, string expectedResult) 
            => EntityNameConvertor.ConvertName(originalName, policy).Should().Be(expectedResult);

        [Theory]
        [InlineData("a", EntityNamingPolicy.AsIs, "a")]
        [InlineData("a", EntityNamingPolicy.UpperCase, "A")]
        [InlineData("entity", EntityNamingPolicy.AsIs, "entity")]
        [InlineData("entity", EntityNamingPolicy.UpperCase, "ENTITIES")]
        [InlineData("box", EntityNamingPolicy.UpperCase, "BOXES")]
        [InlineData("crack", EntityNamingPolicy.UpperCase, "CRACKS")]
        [InlineData("igloo", EntityNamingPolicy.LowerCaseWithUnderscores, "igloos")]
        [InlineData("RottenPotato", EntityNamingPolicy.LowerCaseWithUnderscores, "rotten_potatoes")]
        public void ConvertTableName(string originalName, EntityNamingPolicy? policy, string expectedResult) 
            => EntityNameConvertor.ConvertTableName(originalName, policy).Should().Be(expectedResult);

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

        sealed class NamingScope : IDisposable
        {
            private readonly string mScope;

            public NamingScope(string scope, EntityNamingPolicy policy)
            {
                mScope = scope;
                AllEntities.Inst.NamingPolicy[scope] = policy;
            }

            public void Dispose()
            {
                AllEntities.Inst.ForgetScope(mScope);
                AllEntities.Inst.NamingPolicy[mScope] = EntityNamingPolicy.BackwardCompatibility;

            }
        }

        [Fact]
        public void Policy_BackwardCompatiblity()
        {
            AllEntities.Inst.NamingPolicy.Default.Should().Be(EntityNamingPolicy.BackwardCompatibility);
            AllEntities.Inst.NamingPolicy["naming"].Should().Be(EntityNamingPolicy.BackwardCompatibility);

            using var namingScope = new NamingScope("naming", EntityNamingPolicy.BackwardCompatibility);

            EntityDescriptor descriptor1 = AllEntities.Inst[typeof(TestEntity1)];
            EntityDescriptor descriptor2 = AllEntities.Inst[typeof(TestEntity2)];

            descriptor1.TableDescriptor.Name.Should().Be("TestEntity1");
            descriptor1.TableDescriptor[0].Name.Should().Be("identifier");
            descriptor1.TableDescriptor[1].Name.Should().Be("testfield");
            descriptor1.TableDescriptor[2].Name.Should().Be("reference");

            descriptor2.TableDescriptor.Name.Should().Be("TestEntity2");
            descriptor2.TableDescriptor[0].Name.Should().Be("identifier");

            AllEntities.Inst.ForgetScope("naming");
        }

        [Fact]
        public void Policy_AsIs()
        {
            using var namingScope = new NamingScope("naming", EntityNamingPolicy.AsIs);
            EntityDescriptor descriptor1 = AllEntities.Inst[typeof(TestEntity1)];
            EntityDescriptor descriptor2 = AllEntities.Inst[typeof(TestEntity2)];

            descriptor1.TableDescriptor.Name.Should().Be("TestEntity1");
            descriptor1.TableDescriptor[0].Name.Should().Be("Id");
            descriptor1.TableDescriptor[1].Name.Should().Be("TestField");
            descriptor1.TableDescriptor[2].Name.Should().Be("TestEntity2Ref");


            descriptor2.TableDescriptor.Name.Should().Be("TestEntity2", "TestEntity2 has forced backward compatiblity naming");
            descriptor2.TableDescriptor[0].Name.Should().Be("identifier");
        }

        [Fact]
        public void Policy_LowerCaseWithUnderscores()
        {
            using var namingScope = new NamingScope("naming", EntityNamingPolicy.LowerCaseWithUnderscores);
            EntityDescriptor descriptor1 = AllEntities.Inst[typeof(TestEntity1)];
            EntityDescriptor descriptor2 = AllEntities.Inst[typeof(TestEntity2)];

            descriptor1.TableDescriptor.Name.Should().Be("test_entity1s");
            descriptor1.TableDescriptor[0].Name.Should().Be("id");
            descriptor1.TableDescriptor[1].Name.Should().Be("test_field");
            descriptor1.TableDescriptor[2].Name.Should().Be("test_entity2_ref");


            descriptor2.TableDescriptor.Name.Should().Be("TestEntity2", "TestEntity2 has forced backward compatiblity naming");
            descriptor2.TableDescriptor[0].Name.Should().Be("identifier");
        }

        [Fact]
        public void Policy_LowerCase()
        {
            using var namingScope = new NamingScope("naming", EntityNamingPolicy.LowerCase);
            EntityDescriptor descriptor1 = AllEntities.Inst[typeof(TestEntity1)];
            EntityDescriptor descriptor2 = AllEntities.Inst[typeof(TestEntity2)];

            descriptor1.TableDescriptor.Name.Should().Be("testentity1s");
            descriptor1.TableDescriptor[0].Name.Should().Be("id");
            descriptor1.TableDescriptor[1].Name.Should().Be("testfield");
            descriptor1.TableDescriptor[2].Name.Should().Be("testentity2ref");


            descriptor2.TableDescriptor.Name.Should().Be("TestEntity2", "TestEntity2 has forced backward compatiblity naming");
            descriptor2.TableDescriptor[0].Name.Should().Be("identifier");
        }

        /*
            

            AllEntities.Inst.ForgetScope("naming");
            AllEntities.Inst.NamingPolicy["naming"] = EntityNamingPolicy.LowerCaseWithUnderscores;
            descriptor1 = AllEntities.Inst[typeof(TestEntity1)];
            descriptor2 = AllEntities.Inst[typeof(TestEntity2)];
            var descriptor3 = AllEntities.Inst[typeof(TestEntity3)];
            Assert.AreEqual("test_entity1s", descriptor1.TableDescriptor.Name);
            Assert.AreEqual("specialTestName", descriptor3.TableDescriptor.Name);
            Assert.AreEqual("id", descriptor1.TableDescriptor[0].Name);
            Assert.AreEqual("test_field", descriptor1.TableDescriptor[1].Name);
            Assert.AreEqual("test_entity2_ref", descriptor1.TableDescriptor[2].Name);
            Assert.AreEqual("TestEntity2", descriptor2.TableDescriptor.Name);
            Assert.AreEqual("identifier", descriptor2.TableDescriptor[0].Name);
        */


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

        [Fact]
        public void InRealDB()
        {
            using var connection = SqliteDbConnectionFactory.CreateMemory();
            AllEntities.Inst.NamingPolicy["naming2"] = EntityNamingPolicy.LowerCaseWithUnderscores;
            CreateEntityController controller = new CreateEntityController(typeof(TestObject), "naming2");
            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update);
            var schema = connection.Schema();

            schema.Should().Contain(d => d.Name == "test_objects");
            schema.Should().Contain(d => d.Name == "test_entities");
            schema.Should().Contain(d => d.Name == "men");
        }
    }
}
