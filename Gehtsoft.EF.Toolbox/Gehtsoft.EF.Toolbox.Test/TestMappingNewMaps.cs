using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Mapper;
using NUnit.Framework;

namespace Gehtsoft.EF.Toolbox.Test
{
    [TestFixture]
    public class TestMappingNewMaps
    {
        public enum EntityEnum
        {
            Value1,
            Value2,
            Value3,
        }

        [Entity]
        public class Entity1
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        }

        [MapEntity(typeof(Entity1))]
        public class Entity1Model
        {
            [MapProperty]
            public int ID { get; set; }
            [MapProperty]
            public string Name { get; set; }
        }

        [Entity]
        public class AggEntity
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }
        }

        [Entity]
        public class Entity2
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(ForeignKey = true)]
            public Entity1 Entity1 { get; set; }

            [EntityProperty]
            public string StringValue1 { get; set; }

            [EntityProperty]
            public string StringValue2 { get; set; }

            [EntityProperty]
            public int IntegerValue { get; set; }

            [EntityProperty(DbType = DbType.Int32, Nullable = true)]
            public EntityEnum? EnumValue { get; set; }

            public AggEntity[] Aggregates { get; set; }
        }

        [MapEntity(typeof(AggEntity))]
        public class AggModel
        {
            [MapProperty(Name = nameof(AggEntity.ID))]
            public int Ident { get; set; }
        }

        [MapEntity(EntityType = typeof(Entity2))]
        public class Entity2Model
        {
            [MapProperty]
            public int? ID { get; set; }

            [MapProperty(Name = nameof(Entity2.Entity1))]
            public int? Reference { get; set; }

            public string ReferenceName { get; set; }

            [MapProperty(MapFlags = MapFlag.TrimStrings)]
            public string StringValue1 { get; set; }

            [MapProperty]
            public string StringValue2 { get; set; }

            [MapProperty]
            public decimal IntegerValue { get; set; }

            [MapProperty]
            public int? EnumValue { get; set; }

            [MapProperty]
            public AggModel[] Aggregates { get; set; }
        }

        [MapEntity(EntityType = typeof(Entity2))]
        public class Entity2Model2
        {
            [MapProperty]
            public int? ID { get; set; }

            [MapProperty(Name = nameof(Entity2.Entity1))]
            public int? Reference { get; set; }

            [MapProperty(MapFlags = MapFlag.TrimStrings)]
            public string StringValue1 { get; set; }

            [MapProperty]
            public string StringValue2 { get; set; }

            [MapProperty]
            public decimal IntegerValue { get; set; }

            [MapProperty]
            public int? EnumValue { get; set; }
        }

        [MapEntity(EntityType = typeof(Entity2))]
        public class Entity2Model3
        {
            [MapProperty]
            public int? ID { get; set; }

            [MapProperty(Name = nameof(Entity2.Entity1))]
            public Entity1Model Reference { get; set; }
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            var map = MapFactory.GetMap<Entity2, Entity2Model>();
            map.For(d => d.ReferenceName).From(s => s.Entity1.Name).When(s => s.Entity1 != null);
        }

        [Test]
        public void TestMapping1()
        {
            Entity2 entity;
            Entity2Model model;

            entity = new Entity2() { ID = 5, Entity1 = new Entity1() { ID = 10, Name = "Name10" }, StringValue1 = " 1 ", StringValue2 = " 2 ", IntegerValue = 123, EnumValue = EntityEnum.Value3 };
            model = MapFactory.Map<Entity2, Entity2Model>(entity);
            Assert.IsNotNull(model);
            Assert.AreEqual(5, model.ID);
            Assert.AreEqual(10, model.Reference);
            Assert.AreEqual("Name10", model.ReferenceName);
            Assert.AreEqual("1", model.StringValue1);
            Assert.AreEqual(" 2 ", model.StringValue2);
            Assert.AreEqual(123m, model.IntegerValue);
            Assert.AreEqual((int)EntityEnum.Value3, model.EnumValue);
            Assert.IsNull(model.Aggregates);

            model.StringValue1 = "  1   ";
            entity = MapFactory.Map<Entity2Model, Entity2>(model);
            Assert.AreEqual(5, entity.ID);
            Assert.IsNotNull(entity.Entity1);
            Assert.AreEqual(10, entity.Entity1.ID);
            Assert.AreEqual("1", entity.StringValue1);
            Assert.AreEqual(" 2 ", entity.StringValue2);
            Assert.AreEqual(123, entity.IntegerValue);
            Assert.AreEqual(EntityEnum.Value3, entity.EnumValue);

            model = new Entity2Model();
            entity = MapFactory.Map<Entity2Model, Entity2>(model);
            Assert.AreEqual(0, entity.ID);
            Assert.IsNull(entity.Entity1);
            Assert.IsNull(entity.StringValue1);
            Assert.IsNull(entity.StringValue2);
            Assert.AreEqual(0, entity.IntegerValue);
            Assert.IsNull(entity.EnumValue);
            Assert.IsNull(entity.Aggregates);

            entity = new Entity2()
            {
                Aggregates = new AggEntity[]
                {
                    new AggEntity() {ID = 100},
                    new AggEntity() {ID = 200},
                    new AggEntity() {ID = 300},
                    new AggEntity() {ID = 400},
                    new AggEntity() {ID = 500},
                }
            };

            model = MapFactory.Map<Entity2, Entity2Model>(entity);
            Assert.IsNotNull(model.Aggregates);
            Assert.AreEqual(5, model.Aggregates.Length);
            for (int i = 0; i < 5; i++)
            {
                Assert.IsNotNull(model.Aggregates[i]);
                Assert.AreEqual((i + 1) * 100, model.Aggregates[i].Ident);
            }

            entity = MapFactory.Map<Entity2Model, Entity2>(model);
            Assert.IsNotNull(entity.Aggregates);
            Assert.AreEqual(5, entity.Aggregates.Length);
            for (int i = 0; i < 5; i++)
            {
                Assert.IsNotNull(entity.Aggregates[i]);
                Assert.AreEqual((i + 1) * 100, entity.Aggregates[i].ID);
            }
        }

        [Test]
        public void TestMapping2()
        {
            Entity2 entity = new Entity2() { ID = 100, Entity1 = new Entity1() { ID = 500, Name = "Hello" } };
            Entity2Model2 model2 = MapFactory.Map<Entity2, Entity2Model2>(entity);
            Entity2Model3 model3 = MapFactory.Map<Entity2, Entity2Model3>(entity);
            Assert.AreEqual(500, model2.Reference);
            Assert.AreEqual(500, model3.Reference.ID);
            Assert.AreEqual("Hello", model3.Reference.Name);

            model2.Reference = 10;
            entity = MapFactory.Map<Entity2Model2, Entity2>(model2);
            Assert.AreEqual(10, entity.Entity1.ID);
            Assert.IsNull(entity.Entity1.Name);
            model3.Reference.ID = 15;
            model3.Reference.Name = "Newname";
            entity = MapFactory.Map<Entity2Model3, Entity2>(model3);
            Assert.AreEqual(15, entity.Entity1.ID);
            Assert.AreEqual("Newname", entity.Entity1.Name);
        }

        [Test]
        public void TestActions()
        {
            bool f1, f2;
            Map<Entity2, Entity2Model2> map1 = MapFactory.GetMap<Entity2, Entity2Model2>();
            Map<Entity2Model2, Entity2> map2 = MapFactory.GetMap<Entity2Model2, Entity2>();

            map1.BeforeMapping((e, m) =>
            {
                Assert.IsNull(m.ID);
                f1 = true;
            });

            map1.AfterMapping((e, m) =>
            {
                Assert.AreEqual(e.ID, m.ID);
                f2 = true;
            });

            map2.BeforeMapping((e, m) =>
            {
                Assert.AreEqual(0, m.ID);
                f1 = true;
            });

            map2.AfterMapping((m, e) =>
            {
                Assert.AreEqual(m.ID, e.ID);
                f2 = true;
            });

            f1 = f2 = false;
            MapFactory.Map<Entity2, Entity2Model2>(new Entity2() { ID = 5 });
            Assert.IsTrue(f1);
            Assert.IsTrue(f2);

            f1 = f2 = false;
            MapFactory.Map<Entity2Model2, Entity2>(new Entity2Model2() { ID = 5 });
            Assert.IsTrue(f1);
            Assert.IsTrue(f2);
        }

        public class SourceClass
        {
            public string Name { get; set; }
            public int[] Values { get; set; }
        }

        [MapClass(typeof(SourceClass))]
        public class Model
        {
            [MapProperty]
            public string Name { get; set; }

            [MapProperty(nameof(SourceClass.Values))]
            public List<int> ValueList { get; set; }
        }

        [Test]
        public void TestClassMapping()
        {
            SourceClass sourceClass, sourceClass1;
            Model model;

            sourceClass = new SourceClass() { Name = "Yeahname", Values = new int[] { 1, 2, 3 } };
            model = MapFactory.Map<SourceClass, Model>(sourceClass);
            Assert.IsNotNull(model);
            Assert.AreEqual("Yeahname", model.Name);
            Assert.AreEqual(3, model.ValueList.Count);
            Assert.AreEqual(1, model.ValueList[0]);
            Assert.AreEqual(2, model.ValueList[1]);
            Assert.AreEqual(3, model.ValueList[2]);

            sourceClass1 = MapFactory.Map<Model, SourceClass>(model);
            Assert.AreEqual("Yeahname", sourceClass1.Name);
            Assert.AreEqual(3, sourceClass1.Values.Length);
            Assert.AreEqual(1, sourceClass1.Values[0]);
            Assert.AreEqual(2, sourceClass1.Values[1]);
            Assert.AreEqual(3, sourceClass1.Values[2]);
        }

        [Test]
        public void TestArrayMapping()
        {
            SourceClass[] sourceClasses = new SourceClass[]
            {
                new SourceClass() {Name = "one", Values = null},
                new SourceClass() {Name = "two", Values = new int[] {4, 5}},
                new SourceClass() {Name = "three", Values = new int [] {6, 7, 8, 9}},
            };

            Model[] models = MapFactory.Map<SourceClass[], Model[]>(sourceClasses);
            Assert.IsNotNull(models);
            Assert.AreEqual(sourceClasses.Length, models.Length);
            for (int i = 0; i < sourceClasses.Length; i++)
            {
                Assert.AreEqual(sourceClasses[i].Name, models[i].Name);
                if (sourceClasses[i].Values == null)
                    Assert.IsNull(models[i].ValueList);
                else
                {
                    Assert.AreEqual(sourceClasses[i].Values.Length, models[i].ValueList.Count);
                    Assert.AreEqual(sourceClasses[i].Values, models[i].ValueList);
                }
            }
        }

        [Test]
        public void TestClassToArrayMapping()
        {
            Map<SourceClass, string[]> map = MapFactory.CreateMap<SourceClass, string[]>();
            map.Factory = s => s == null ? null : new string[1];
            map.AfterMapping((s, d) => d[0] = s.Name).When((s, d) => s != null);
            SourceClass cls = null;

            Assert.IsNull(MapFactory.Map<SourceClass, string[]>(cls));

            cls = new SourceClass() { Name = "123" };
            string[] r = MapFactory.Map<SourceClass, string[]>(cls);

            Assert.IsNotNull(r);
            Assert.AreEqual(1, r.Length);
            Assert.AreEqual("123", r[0]);
        }

        public class Entity2Model4
        {
            public int ID { get; set; }
            public string ReferenceName { get; set; }
            public string Field1 { get; set; }
            public int Field2 { get; set; }
            public int Field3 { get; set; }
        }

        [Test]
        public void ManualMapTest()
        {
            {
                var map = MapFactory.CreateMap<Entity2, Entity2Model4>();
                map.For(m => m.ID).From(e => e.ID);
                map.For(m => m.ReferenceName).From(e => e.Entity1.Name).When(e => e.Entity1 != null).Otherwise().Assign("null");
                map.For(m => m.Field1).Assign("123");
                map.For(m => m.Field2).Assign(e => (e?.ID ?? e.ID));
                map.Assign<int>((m, v) => m.Field3 = v).Assign(e => e.ID * 5);
            }

            {
                Entity2 e = new Entity2() { ID = 123, Entity1 = new Entity1() { ID = 456, Name = "MyName" } };
                Entity2Model4 m = MapFactory.Map<Entity2, Entity2Model4>(e);

                Assert.AreEqual(e.ID, m.ID);
                Assert.AreEqual(e.Entity1.Name, m.ReferenceName);
                Assert.AreEqual("123", m.Field1);
                Assert.AreEqual(e.ID, m.Field2);
                Assert.AreEqual(e.ID * 5, m.Field3);

                e = new Entity2() { ID = 123, Entity1 = null };
                m = MapFactory.Map<Entity2, Entity2Model4>(e);

                Assert.AreEqual(e.ID, m.ID);
                Assert.AreEqual("null", m.ReferenceName);
                Assert.AreEqual("123", m.Field1);
                Assert.AreEqual(e.ID, m.Field2);
                Assert.AreEqual(e.ID * 5, m.Field3);
            }
        }

        [Test]
        public void SelfMappingTest()
        {
            Entity2 e1, e2;
            e1 = new Entity2() { ID = 1, Entity1 = new Entity1() { ID = 10 }, Aggregates = new AggEntity[] { new AggEntity() { ID = 20 } } };

            e2 = MapFactory.Map<Entity2, Entity2>(e1);
            Assert.IsTrue(object.ReferenceEquals(e1, e2));

            var map = MapFactory.CreateMap<Entity2, Entity2>();
            Assert.IsFalse(map.ContainsRuleFor(nameof(Entity2.ID)));
            map.MapPropertiesByName();
            Assert.IsTrue(map.ContainsRuleFor(nameof(Entity2.ID)));
            e2 = MapFactory.Map<Entity2, Entity2>(e1);
            Assert.IsFalse(object.ReferenceEquals(e1, e2));
            Assert.AreEqual(e1.ID, e2.ID);
            Assert.IsTrue(object.ReferenceEquals(e1.Entity1, e2.Entity1));
            Assert.IsTrue(object.ReferenceEquals(e1.Aggregates, e2.Aggregates));

            var map1 = MapFactory.CreateMap<Entity1, Entity1>();
            map1.MapPropertiesByName();
            e2 = MapFactory.Map<Entity2, Entity2>(e1);
            Assert.IsFalse(object.ReferenceEquals(e1.Entity1, e2.Entity1));
            Assert.AreEqual(e1.Entity1.ID, e2.Entity1.ID);

            var map2 = MapFactory.CreateMap<AggEntity[], AggEntity[]>();
            map2.Factory = entities => entities == null ? null : new AggEntity[entities.Length];
            map2.AfterMapping((source, destination) =>
            {
                for (int i = 0; i < source.Length; i++) destination[i] = MapFactory.Map<AggEntity, AggEntity>(source[i]);
            }).When((source, destination) => source != null);

            e2 = MapFactory.Map<Entity2, Entity2>(e1);
            Assert.IsFalse(object.ReferenceEquals(e1.Aggregates, e2.Aggregates));
            Assert.AreEqual(e1.Aggregates.Length, e2.Aggregates.Length);
        }

        public class Parent
        {
            public int ID { get; set; }
        }

        public class Child : Parent
        {
            public int ID1 { get; set; }
            public string Name { get; set; }
        }

        [Test]
        public void TestAssignambleReplaceWithAndIgnore()
        {
            //test auto mapping of assignable
            Parent parent;
            Child child;

            {
                child = new Child() { ID = 100 };
                parent = MapFactory.Map<Child, Parent>(child);
                Assert.IsTrue(object.ReferenceEquals(parent, child));

                Map<Child, Parent> map = MapFactory.CreateMap<Child, Parent>();
                map.For(p => p.ID).From(c => c.ID);

                parent = MapFactory.Map<Child, Parent>(child);
                Assert.IsFalse(object.ReferenceEquals(parent, child));
                Assert.AreEqual(child.ID, parent.ID);

                MapFactory.RemoveMap<Child, Parent>();
                parent = MapFactory.Map<Child, Parent>(child);
                Assert.IsTrue(object.ReferenceEquals(parent, child));

                parent = new Parent() { ID = 100 };
                Assert.Throws<ArgumentException>(() => MapFactory.Map<Parent, Child>(parent));
            }

            {
                Map<Parent, Child> map = MapFactory.CreateMap<Parent, Child>();
                bool rule1, rule2, rule3;

                map.For(c => c.ID)
                    .When(p => p.ID == 0)
                    .Assign(p =>
                    {
                        rule1 = true;
                        return 1000;
                    })
                    .Otherwise()
                    .Assign(p =>
                    {
                        rule2 = true;
                        return p.ID;
                    });

                rule1 = rule2 = false;
                parent = new Parent() { ID = 0 };
                child = MapFactory.Map<Parent, Child>(parent);
                Assert.IsTrue(rule1);
                Assert.IsFalse(rule2);
                Assert.AreEqual(1000, child.ID);

                rule1 = rule2 = false;
                parent = new Parent() { ID = 1 };
                child = MapFactory.Map<Parent, Child>(parent);
                Assert.IsFalse(rule1);
                Assert.IsTrue(rule2);
                Assert.AreEqual(1, child.ID);

                map.For(c => c.ID)
                    .ReplaceWith()
                    .Assign(p =>
                    {
                        rule3 = true;
                        return p.ID * 2;
                    });

                rule1 = rule2 = rule3 = false;
                parent = new Parent() { ID = 2 };
                child = MapFactory.Map<Parent, Child>(parent);
                Assert.IsFalse(rule1);
                Assert.IsFalse(rule2);
                Assert.IsTrue(rule3);
                Assert.AreEqual(4, child.ID);
            }
        }

        public class NullableClass1
        {
            public int? Value1 { get; set; }
            public string Value2 { get; set; }
        }

        [MapClass(typeof(NullableClass1))]
        public class NullableClass2
        {
            [MapProperty]
            public string Value1 { get; set; }
            [MapProperty]
            public string Value2 { get; set; }
        }
        [Test]
        public void MappingOfNull()
        {
            NullableClass1 cls1;
            NullableClass2 cls2;

            cls1 = new NullableClass1() { Value1 = 10, Value2 = "abc" };
            cls2 = MapFactory.Map<NullableClass1, NullableClass2>(cls1);
            Assert.AreEqual("10", cls2.Value1);
            Assert.AreEqual("abc", cls2.Value2);

            cls2 = new NullableClass2() { Value1 = "15", Value2 = "def" };
            cls1 = MapFactory.Map<NullableClass2, NullableClass1>(cls2);
            Assert.AreEqual(15, cls1.Value1);
            Assert.AreEqual("def", cls1.Value2);

            cls1 = new NullableClass1() { Value1 = 1, Value2 = "2" };
            cls2 = new NullableClass2() { Value1 = "123", Value2 = "456" };
            MapFactory.Map(cls1, cls2);
            Assert.AreEqual("1", cls2.Value1);
            Assert.AreEqual("2", cls2.Value2);

            cls1.Value1 = null;
            cls1.Value2 = null;
            MapFactory.GetMap<NullableClass1, NullableClass2>().Do(cls1, cls2, true);
            Assert.AreEqual("1", cls2.Value1);
            Assert.AreEqual("2", cls2.Value2);

            MapFactory.GetMap<NullableClass1, NullableClass2>().Do(cls1, cls2, false);
            Assert.AreEqual(null, cls2.Value1);
            Assert.AreEqual(null, cls2.Value2);
        }
    }
}
