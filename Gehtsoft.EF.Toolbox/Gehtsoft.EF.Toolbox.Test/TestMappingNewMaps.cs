using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Mapper;
using AwesomeAssertions;
using Xunit;

namespace Gehtsoft.EF.Toolbox.Test
{
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

        static TestMappingNewMaps()
        {
            var map = MapFactory.GetMap<Entity2, Entity2Model>();
            map.For(d => d.ReferenceName).From(s => s.Entity1.Name).When(s => s.Entity1 != null);
        }

        [Fact]
        public void TestMapping1()
        {
            Entity2 entity;
            Entity2Model model;

            entity = new Entity2() { ID = 5, Entity1 = new Entity1() { ID = 10, Name = "Name10" }, StringValue1 = " 1 ", StringValue2 = " 2 ", IntegerValue = 123, EnumValue = EntityEnum.Value3 };
            model = MapFactory.Map<Entity2, Entity2Model>(entity);
            model.Should().NotBeNull();
            model.ID.Should().Be(5);
            model.Reference.Should().Be(10);
            model.ReferenceName.Should().Be("Name10");
            model.StringValue1.Should().Be("1");
            model.StringValue2.Should().Be(" 2 ");
            model.IntegerValue.Should().Be(123m);
            model.EnumValue.Should().Be((int)EntityEnum.Value3);
            model.Aggregates.Should().BeNull();

            model.StringValue1 = "  1   ";
            entity = MapFactory.Map<Entity2Model, Entity2>(model);
            entity.ID.Should().Be(5);
            entity.Entity1.Should().NotBeNull();
            entity.Entity1.ID.Should().Be(10);
            entity.StringValue1.Should().Be("1");
            entity.StringValue2.Should().Be(" 2 ");
            entity.IntegerValue.Should().Be(123);
            entity.EnumValue.Should().Be(EntityEnum.Value3);

            model = new Entity2Model();
            entity = MapFactory.Map<Entity2Model, Entity2>(model);
            entity.ID.Should().Be(0);
            entity.Entity1.Should().BeNull();
            entity.StringValue1.Should().BeNull();
            entity.StringValue2.Should().BeNull();
            entity.IntegerValue.Should().Be(0);
            entity.EnumValue.Should().BeNull();
            entity.Aggregates.Should().BeNull();

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
            model.Aggregates.Should().NotBeNull();
            model.Aggregates.Length.Should().Be(5);
            for (int i = 0; i < 5; i++)
            {
                model.Aggregates[i].Should().NotBeNull();
                model.Aggregates[i].Ident.Should().Be((i + 1) * 100);
            }

            entity = MapFactory.Map<Entity2Model, Entity2>(model);
            entity.Aggregates.Should().NotBeNull();
            entity.Aggregates.Length.Should().Be(5);
            for (int i = 0; i < 5; i++)
            {
                entity.Aggregates[i].Should().NotBeNull();
                entity.Aggregates[i].ID.Should().Be((i + 1) * 100);
            }
        }

        [Fact]
        public void TestMapping2()
        {
            Entity2 entity = new Entity2() { ID = 100, Entity1 = new Entity1() { ID = 500, Name = "Hello" } };
            Entity2Model2 model2 = MapFactory.Map<Entity2, Entity2Model2>(entity);
            Entity2Model3 model3 = MapFactory.Map<Entity2, Entity2Model3>(entity);
            model2.Reference.Should().Be(500);
            model3.Reference.ID.Should().Be(500);
            model3.Reference.Name.Should().Be("Hello");

            model2.Reference = 10;
            entity = MapFactory.Map<Entity2Model2, Entity2>(model2);
            entity.Entity1.ID.Should().Be(10);
            entity.Entity1.Name.Should().BeNull();
            model3.Reference.ID = 15;
            model3.Reference.Name = "Newname";
            entity = MapFactory.Map<Entity2Model3, Entity2>(model3);
            entity.Entity1.ID.Should().Be(15);
            entity.Entity1.Name.Should().Be("Newname");
        }

        [Fact]
        public void TestActions()
        {
            bool f1, f2;
            Map<Entity2, Entity2Model2> map1 = MapFactory.GetMap<Entity2, Entity2Model2>();
            Map<Entity2Model2, Entity2> map2 = MapFactory.GetMap<Entity2Model2, Entity2>();

            map1.BeforeMapping((e, m) =>
            {
                m.ID.Should().BeNull();
                f1 = true;
            });

            map1.AfterMapping((e, m) =>
            {
                m.ID.Should().Be(e.ID);
                f2 = true;
            });

            map2.BeforeMapping((e, m) =>
            {
                m.ID.Should().Be(0);
                f1 = true;
            });

            map2.AfterMapping((m, e) =>
            {
                e.ID.Should().Be(m.ID);
                f2 = true;
            });

            f1 = f2 = false;
            MapFactory.Map<Entity2, Entity2Model2>(new Entity2() { ID = 5 });
            f1.Should().BeTrue();
            f2.Should().BeTrue();

            f1 = f2 = false;
            MapFactory.Map<Entity2Model2, Entity2>(new Entity2Model2() { ID = 5 });
            f1.Should().BeTrue();
            f2.Should().BeTrue();
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

        [Fact]
        public void TestClassMapping()
        {
            SourceClass sourceClass, sourceClass1;
            Model model;

            sourceClass = new SourceClass() { Name = "Yeahname", Values = new int[] { 1, 2, 3 } };
            model = MapFactory.Map<SourceClass, Model>(sourceClass);
            model.Should().NotBeNull();
            model.Name.Should().Be("Yeahname");
            model.ValueList.Count.Should().Be(3);
            model.ValueList[0].Should().Be(1);
            model.ValueList[1].Should().Be(2);
            model.ValueList[2].Should().Be(3);

            sourceClass1 = MapFactory.Map<Model, SourceClass>(model);
            sourceClass1.Name.Should().Be("Yeahname");
            sourceClass1.Values.Length.Should().Be(3);
            sourceClass1.Values[0].Should().Be(1);
            sourceClass1.Values[1].Should().Be(2);
            sourceClass1.Values[2].Should().Be(3);
        }

        [Fact]
        public void TestArrayMapping()
        {
            SourceClass[] sourceClasses = new SourceClass[]
            {
                new SourceClass() {Name = "one", Values = null},
                new SourceClass() {Name = "two", Values = new int[] {4, 5}},
                new SourceClass() {Name = "three", Values = new int [] {6, 7, 8, 9}},
            };

            Model[] models = MapFactory.Map<SourceClass[], Model[]>(sourceClasses);
            models.Should().NotBeNull();
            models.Length.Should().Be(sourceClasses.Length);
            for (int i = 0; i < sourceClasses.Length; i++)
            {
                models[i].Name.Should().Be(sourceClasses[i].Name);
                if (sourceClasses[i].Values == null)
                    models[i].ValueList.Should().BeNull();
                else
                {
                    models[i].ValueList.Count.Should().Be(sourceClasses[i].Values.Length);
                    models[i].ValueList.Should().Equal(sourceClasses[i].Values);
                }
            }
        }

        [Fact]
        public void TestClassToArrayMapping()
        {
            Map<SourceClass, string[]> map = MapFactory.CreateMap<SourceClass, string[]>();
            map.Factory = s => s == null ? null : new string[1];
            map.AfterMapping((s, d) => d[0] = s.Name).When((s, d) => s != null);
            SourceClass cls = null;

            MapFactory.Map<SourceClass, string[]>(cls).Should().BeNull();

            cls = new SourceClass() { Name = "123" };
            string[] r = MapFactory.Map<SourceClass, string[]>(cls);

            r.Should().NotBeNull();
            r.Length.Should().Be(1);
            r[0].Should().Be("123");
        }

        public class Entity2Model4
        {
            public int ID { get; set; }
            public string ReferenceName { get; set; }
            public string Field1 { get; set; }
            public int Field2 { get; set; }
            public int Field3 { get; set; }
        }

        [Fact]
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

                m.ID.Should().Be(e.ID);
                m.ReferenceName.Should().Be(e.Entity1.Name);
                m.Field1.Should().Be("123");
                m.Field2.Should().Be(e.ID);
                m.Field3.Should().Be(e.ID * 5);

                e = new Entity2() { ID = 123, Entity1 = null };
                m = MapFactory.Map<Entity2, Entity2Model4>(e);

                m.ID.Should().Be(e.ID);
                m.ReferenceName.Should().Be("null");
                m.Field1.Should().Be("123");
                m.Field2.Should().Be(e.ID);
                m.Field3.Should().Be(e.ID * 5);
            }
        }

        [Fact]
        public void SelfMappingTest()
        {
            Entity2 e1, e2;
            e1 = new Entity2() { ID = 1, Entity1 = new Entity1() { ID = 10 }, Aggregates = new AggEntity[] { new AggEntity() { ID = 20 } } };

            e2 = MapFactory.Map<Entity2, Entity2>(e1);
            object.ReferenceEquals(e1, e2).Should().BeTrue();

            var map = MapFactory.CreateMap<Entity2, Entity2>();
            map.ContainsRuleFor(nameof(Entity2.ID)).Should().BeFalse();
            map.MapPropertiesByName();
            map.ContainsRuleFor(nameof(Entity2.ID)).Should().BeTrue();
            e2 = MapFactory.Map<Entity2, Entity2>(e1);
            object.ReferenceEquals(e1, e2).Should().BeFalse();
            e2.ID.Should().Be(e1.ID);
            object.ReferenceEquals(e1.Entity1, e2.Entity1).Should().BeTrue();
            object.ReferenceEquals(e1.Aggregates, e2.Aggregates).Should().BeTrue();

            var map1 = MapFactory.CreateMap<Entity1, Entity1>();
            map1.MapPropertiesByName();
            e2 = MapFactory.Map<Entity2, Entity2>(e1);
            object.ReferenceEquals(e1.Entity1, e2.Entity1).Should().BeFalse();
            e2.Entity1.ID.Should().Be(e1.Entity1.ID);

            var map2 = MapFactory.CreateMap<AggEntity[], AggEntity[]>();
            map2.Factory = entities => entities == null ? null : new AggEntity[entities.Length];
            map2.AfterMapping((source, destination) =>
            {
                for (int i = 0; i < source.Length; i++) destination[i] = MapFactory.Map<AggEntity, AggEntity>(source[i]);
            }).When((source, destination) => source != null);

            e2 = MapFactory.Map<Entity2, Entity2>(e1);
            object.ReferenceEquals(e1.Aggregates, e2.Aggregates).Should().BeFalse();
            e2.Aggregates.Length.Should().Be(e1.Aggregates.Length);
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

        [Fact]
        public void TestAssignambleReplaceWithAndIgnore()
        {
            //test auto mapping of assignable
            Parent parent;
            Child child;

            {
                child = new Child() { ID = 100 };
                parent = MapFactory.Map<Child, Parent>(child);
                object.ReferenceEquals(parent, child).Should().BeTrue();

                Map<Child, Parent> map = MapFactory.CreateMap<Child, Parent>();
                map.For(p => p.ID).From(c => c.ID);

                parent = MapFactory.Map<Child, Parent>(child);
                object.ReferenceEquals(parent, child).Should().BeFalse();
                parent.ID.Should().Be(child.ID);

                MapFactory.RemoveMap<Child, Parent>();
                parent = MapFactory.Map<Child, Parent>(child);
                object.ReferenceEquals(parent, child).Should().BeTrue();

                parent = new Parent() { ID = 100 };
                ((Action)(() => MapFactory.Map<Parent, Child>(parent))).Should().Throw<ArgumentException>();
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
                rule1.Should().BeTrue();
                rule2.Should().BeFalse();
                child.ID.Should().Be(1000);

                rule1 = rule2 = false;
                parent = new Parent() { ID = 1 };
                child = MapFactory.Map<Parent, Child>(parent);
                rule1.Should().BeFalse();
                rule2.Should().BeTrue();
                child.ID.Should().Be(1);

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
                rule1.Should().BeFalse();
                rule2.Should().BeFalse();
                rule3.Should().BeTrue();
                child.ID.Should().Be(4);
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
        [Fact]
        public void MappingOfNull()
        {
            NullableClass1 cls1;
            NullableClass2 cls2;

            cls1 = new NullableClass1() { Value1 = 10, Value2 = "abc" };
            cls2 = MapFactory.Map<NullableClass1, NullableClass2>(cls1);
            cls2.Value1.Should().Be("10");
            cls2.Value2.Should().Be("abc");

            cls2 = new NullableClass2() { Value1 = "15", Value2 = "def" };
            cls1 = MapFactory.Map<NullableClass2, NullableClass1>(cls2);
            cls1.Value1.Should().Be(15);
            cls1.Value2.Should().Be("def");

            cls1 = new NullableClass1() { Value1 = 1, Value2 = "2" };
            cls2 = new NullableClass2() { Value1 = "123", Value2 = "456" };
            MapFactory.Map(cls1, cls2);
            cls2.Value1.Should().Be("1");
            cls2.Value2.Should().Be("2");

            cls1.Value1 = null;
            cls1.Value2 = null;
            MapFactory.GetMap<NullableClass1, NullableClass2>().Do(cls1, cls2, true);
            cls2.Value1.Should().Be("1");
            cls2.Value2.Should().Be("2");

            MapFactory.GetMap<NullableClass1, NullableClass2>().Do(cls1, cls2, false);
            cls2.Value1.Should().Be(null);
            cls2.Value2.Should().Be(null);
        }
    }
}
