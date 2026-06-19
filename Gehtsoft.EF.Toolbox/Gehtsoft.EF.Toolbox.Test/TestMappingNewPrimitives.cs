using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Mapper;
using Gehtsoft.Tools2.Extensions;
using AwesomeAssertions;
using Xunit;

namespace Gehtsoft.EF.Toolbox.Test
{
    public class TestMappingNewPrimitives
    {
        public class Model1
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public DateTime? DateTime { get; set; }
            public double? Number { get; set; }
        }

        [Entity]
        public class Entity1
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }
            [EntityProperty(Size = 32)]
            public string Title { get; set; }
            [EntityProperty(DbType = DbType.Date)]
            public DateTime DateTime { get; set; }
            [EntityProperty(Size = 8, Precision = 2, Nullable = true)]
            public double? Number { get; set; }

            public double? SquareRoot { get; set; }
            public bool? HasSquareRoot { get; set; } = null;
        }

        private class Model2
        {
            public int ID { get; set; }
            public int? Reference { get; set; }
        }

        [Entity]
        public class Entity2
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }
            [EntityProperty(ForeignKey = true)]
            public Entity1 Reference { get; set; }
        }

        [Fact]
        public void TestTypeRecognizer()
        {
            ValueMapper.GetElementType(typeof(int)).Should().Be(null);
            ValueMapper.GetElementType(typeof(string)).Should().Be(typeof(char));
            ValueMapper.GetElementType(typeof(EntityCollection<string>)).Should().Be(typeof(string));
            ValueMapper.GetElementType(typeof(List<double?>)).Should().Be(typeof(double?));
            ValueMapper.GetElementType(typeof(DateTime?[])).Should().Be(typeof(DateTime?));
            ValueMapper.GetElementType(typeof(EntityCollection<string[]>)).Should().Be(typeof(string[]));
            ValueMapper.GetElementType(typeof(Queue<List<int>>)).Should().Be(typeof(List<int>));
        }

        [Fact]
        public void TestProperty()
        {
            Model1 model1 = new Model1();

            ClassPropertyAccessor mapping = new ClassPropertyAccessor(typeof(Model1).GetTypeInfo().GetProperty(nameof(Model1.ID)));
            mapping.Name.Should().Be(nameof(Model1.ID));
            mapping.ValueType.Should().Be(typeof(int));
            mapping.Set(model1, 10);
            model1.ID.Should().Be(10);
            model1.ID = 15;
            mapping.Get(model1).Should().Be(15);

            ClassPropertyAccessor mapping1 = null;
            mapping.Equals(mapping1).Should().BeFalse();
            mapping1 = mapping;
            mapping.Equals(mapping1).Should().BeTrue();
            mapping1 = new ClassPropertyAccessor(typeof(Model1).GetTypeInfo().GetProperty(nameof(Model1.ID)));
            mapping.Equals(mapping1).Should().BeTrue();
            ReferenceEquals(mapping, mapping1).Should().BeFalse();
            mapping1 = new ClassPropertyAccessor(typeof(Model1).GetTypeInfo().GetProperty(nameof(Model1.Name)));
            mapping.Equals(mapping1).Should().BeFalse();
        }

        [Fact]
        public void TestEntityProperty()
        {
            Entity1 entity1 = new Entity1();
            EntityDescriptor entity1Descriptor = AllEntities.Inst[typeof(Entity1)];

            EntityPropertyAccessor accessor = new EntityPropertyAccessor(entity1Descriptor[nameof(Entity1.ID)]);
            accessor.Name.Should().Be(nameof(Entity1.ID));
            accessor.ValueType.Should().Be(typeof(int));
            accessor.Set(entity1, 10);
            entity1.ID.Should().Be(10);
            entity1.ID = 15;
            accessor.Get(entity1).Should().Be(15);

            EntityPropertyAccessor mapping1 = null;
            accessor.Equals(mapping1).Should().BeFalse();
            mapping1 = accessor;
            accessor.Equals(mapping1).Should().BeTrue();
            mapping1 = new EntityPropertyAccessor(entity1Descriptor[nameof(Entity1.ID)]);
            accessor.Equals(mapping1).Should().BeTrue();
            ReferenceEquals(accessor, mapping1).Should().BeFalse();
            mapping1 = new EntityPropertyAccessor(entity1Descriptor[nameof(Entity1.Title)]);
            accessor.Equals(mapping1).Should().BeFalse();

            Entity2 entity2 = new Entity2();
            EntityDescriptor entity2Descriptor = AllEntities.Inst[typeof(Entity2)];
            EntityPrimaryKeySource pksource = new EntityPrimaryKeySource(entity2Descriptor[nameof(Entity2.Reference)]);
            pksource.Name.Should().Be(nameof(Entity2.Reference));
            pksource.ValueType.Should().Be(typeof(int));
            pksource.Get(entity2).Should().BeNull();

            entity2.Reference = entity1;
            pksource.Get(entity2).Should().Be(15);

            Model2 model2 = new Model2();
            ModelPrimaryKeySource pksource1 = new ModelPrimaryKeySource(entity2Descriptor[nameof(Entity2.Reference)], model2.GetType().GetTypeInfo().GetProperty(nameof(Model2.Reference)));
            pksource1.Name.Should().Be(nameof(Model2.Reference));
            pksource1.ValueType.Should().Be(typeof(Entity1));
            pksource1.Get(model2).Should().BeNull();
            model2.Reference = 55;
            object ro = pksource1.Get(model2);
            ro.Should().NotBeNull();
            ro.GetType().Should().Be(typeof(Entity1));
            ((ro as Entity1)?.ID).Should().Be(55);
        }

        [Fact]
        public void TestFunctionSource()
        {
            Model1 model1 = new Model1() { ID = 10, Number = null };

            ExpressionSource<Model1, double> expression = new ExpressionSource<Model1, double>(model => model.ID + (model.Number ?? 5));
            expression.ValueType.Should().Be(typeof(double));
            expression.Name.Should().Be("expression");
            model1.ID.Should().Be(10);
            expression.Get(model1).Should().Be(15);

            model1.Number = 55;
            expression.Get(model1).Should().Be(65);
        }

        public enum TestEnum
        {
            E1 = 10,
            E2 = 11,
            E3 = 12,
        }

        [Fact]
        public void TestValueMapper()
        {
            //test null mappings
            ValueMapper.MapValue(null, typeof(Model1)).Should().BeNull();
            ValueMapper.MapValue(null, typeof(int?)).Should().BeNull();
            ValueMapper.MapValue(null, typeof(int)).Should().Be(0);
            ValueMapper.MapValue(null, typeof(DateTime)).Should().Be(new DateTime(0));

            //test self-mappings
            Model1 model1 = new Model1();
            ReferenceEquals(model1, ValueMapper.MapValue(model1, typeof(Model1))).Should().BeTrue();
            ValueMapper.MapValue(10, typeof(int)).Should().Be(10);

            //test value-to-value mappings
            ValueMapper.MapValue(10, typeof(string)).Should().Be("10");
            (ValueMapper.MapValue(10, typeof(string)) is string).Should().BeTrue();
            ValueMapper.MapValue(10, typeof(double)).Should().Be(10.0);
            (ValueMapper.MapValue(10, typeof(double)) is double).Should().BeTrue();

            //test map-to-nullable mappings
            ValueMapper.MapValue(10, typeof(double?)).Should().Be(10.0);
            (ValueMapper.MapValue(10, typeof(double?)) is double).Should().BeTrue();

            //test enum mappings
            ValueMapper.MapValue((int)TestEnum.E1, typeof(TestEnum)).Should().Be(TestEnum.E1);
            ValueMapper.MapValue(nameof(TestEnum.E2), typeof(TestEnum)).Should().Be(TestEnum.E2);
            ValueMapper.MapValue(TestEnum.E3, typeof(TestEnum)).Should().Be(TestEnum.E3);
            ValueMapper.MapValue((int)TestEnum.E1, typeof(TestEnum?)).Should().Be(TestEnum.E1);
            ValueMapper.MapValue(nameof(TestEnum.E2), typeof(TestEnum?)).Should().Be(TestEnum.E2);
            ValueMapper.MapValue(TestEnum.E3, typeof(TestEnum?)).Should().Be(TestEnum.E3);
            ValueMapper.MapValue(null, typeof(TestEnum?)).Should().Be(null);

            //array-to-array mappings
            int[] array1 = new[] { 1, 2, 3, 4, 5 };
            double[] array2;
            object ro = ValueMapper.MapValue(array1, typeof(double[]));
            ro.Should().NotBeNull();
            (ro is double[]).Should().BeTrue();
            array2 = (double[])ro;
            array2.Should().Equal(new double[] { 1, 2, 3, 4, 5 });

            //enumerable-to-array mappings
            List<string> list = new List<string>(new string[] { "1.1", "2.2", "3.3" });
            ro = ValueMapper.MapValue(list, typeof(double[]));
            ro.Should().NotBeNull();
            (ro is double[]).Should().BeTrue();
            array2 = (double[])ro;
            array2.Should().Equal(new double[] { 1.1, 2.2, 3.3 });

            //array-to-list mappings
            ro = ValueMapper.MapValue(array1, typeof(List<string>));
            ro.Should().NotBeNull();
            (ro is List<string>).Should().BeTrue();
            list = (List<string>)ro;
            list.Should().Equal(new string[] { "1", "2", "3", "4", "5" });

            //enumerable-to-list mappings
            Queue<DateTime> queue = new Queue<DateTime>();
            queue.Enqueue(new DateTime(2000, 1, 1));
            queue.Enqueue(new DateTime(2001, 1, 1));
            queue.Enqueue(new DateTime(2002, 1, 1));
            ro = ValueMapper.MapValue(queue, typeof(List<string>));
            ro.Should().NotBeNull();
            (ro is List<string>).Should().BeTrue();
            list = (List<string>)ro;
            list.Should().Equal(new string[] { new DateTime(2000, 1, 1).ToString(CultureInfo.InvariantCulture), new DateTime(2001, 1, 1).ToString(CultureInfo.InvariantCulture), new DateTime(2002, 1, 1).ToString(CultureInfo.InvariantCulture) });

            //flags
            ValueMapper.MapValue(" abcd ", typeof(string), MapFlag.TrimStrings).Should().Be("abcd");
            (((DateTime)ValueMapper.MapValue(DateTime.Now, typeof(DateTime), MapFlag.TrimToSeconds)).Millisecond).Should().Be(0);
            (((DateTime)ValueMapper.MapValue(DateTime.Now, typeof(DateTime), MapFlag.TrimToDate)).Hour).Should().Be(0);
            (((DateTime)ValueMapper.MapValue(DateTime.Now, typeof(DateTime), MapFlag.TrimToDate)).Minute).Should().Be(0);
            (((DateTime)ValueMapper.MapValue(DateTime.Now, typeof(DateTime), MapFlag.TrimToDate)).Second).Should().Be(0);
        }

        [Fact]
        public void PropertyMappingTest()
        {
            PropertyMapping<Model1, Entity1> propertyMapping = new PropertyMapping<Model1, Entity1>(null);
            propertyMapping.From(s => s.ID).To(d => d.ID).When(s => s.ID > 10);
            propertyMapping.Source.Should().NotBeNull();
            (propertyMapping.Source is ClassPropertyAccessor).Should().BeTrue();
            propertyMapping.Target.Should().NotBeNull();
            (propertyMapping.Target is ClassPropertyAccessor).Should().BeTrue();

            Model1 model = new Model1();
            Entity1 entity = new Entity1();

            model.ID = 20;
            propertyMapping.Map(model, entity);
            entity.ID.Should().Be(20);
            model.ID = 10;
            propertyMapping.Map(model, entity);
            entity.ID.Should().Be(20);

            propertyMapping.Ignore();
            model.ID = 25;
            propertyMapping.Map(model, entity);
            entity.ID.Should().Be(20);

            propertyMapping.Always();
            model.ID = 10;
            propertyMapping.Map(model, entity);
            entity.ID.Should().Be(10);

            ((Action)(() => propertyMapping.From(s => s.ID + 0x1000).To(d => d.ID + 50))).Should().Throw<ArgumentException>();
            ((Action)(() => propertyMapping.From(s => s.ID + 0x1000).To("123"))).Should().Throw<ArgumentException>();
            propertyMapping.From(s => s.ID + 0x1000).To(nameof(Entity1.ID)).Always();
            model.ID = 1;
            propertyMapping.Map(model, entity);
            entity.ID.Should().Be(0x1001);
        }

        [Fact]
        public void ManuallyCreatedMapTest()
        {
            Map<Model1, Entity1> map = new Map<Model1, Entity1>();

            map.For(d => d.ID).From(s => s.ID);
            map.For(d => d.Title).From(nameof(Model1.Name));
            map.For(nameof(Entity1.DateTime)).From(s => s.DateTime);
            map.For(d => d.Number).From(s => s.Number);
            map.For(d => d.SquareRoot).From(s => Math.Sqrt(s.Number ?? 0)).When(s => s.Number != null);

            Model1 model1 = new Model1() { ID = 1, Name = "MyName", DateTime = DateTime.Now, Number = 25 };
            Entity1 entity = map.Do(model1);

            entity.Should().NotBeNull();
            entity.ID.Should().Be(1);
            entity.Title.Should().Be("MyName");
            entity.DateTime.Should().Be(model1.DateTime);
            entity.Number.Should().Be(25);
            entity.SquareRoot.Should().Be(5);

            map.Find(d => d.SquareRoot).ForAll(r => r.Ignore());
            entity = map.Do(model1);
            entity.Number.Should().Be(25);
            entity.SquareRoot.Should().Be(null);

            map.Find(d => d.SquareRoot).ForAll(r => r.WhenDestination(d => d.HasSquareRoot != null));
            entity = map.Do(model1);
            entity.Number.Should().Be(25);
            entity.SquareRoot.Should().Be(null);

            map.BeforeMapping((source, destination) => destination.HasSquareRoot = false);
            entity = map.Do(model1);
            entity.Number.Should().Be(25);
            entity.SquareRoot.Should().Be(5);
            entity.HasSquareRoot.Should().BeFalse();

            map.AfterMapping((source, destination) => destination.HasSquareRoot = destination.SquareRoot != null);
            entity = map.Do(model1);
            entity.HasSquareRoot.Should().BeTrue();
        }

        [Fact]
        public void ManuallyCreatedMapTestWithRegistration()
        {
            Map<Model1, Entity1> map = MapFactory.CreateMap<Model1, Entity1>();
            map.For(d => d.ID).From(s => s.ID);
            map.For(d => d.Title).From(nameof(Model1.Name));
            map.For(nameof(Entity1.DateTime)).From(s => s.DateTime);
            map.For(d => d.Number).From(s => s.Number);
            map.For(d => d.SquareRoot).From(s => Math.Sqrt(s.Number ?? 0)).When(s => s.Number != null);
            map.BeforeMapping((source, destination) => destination.HasSquareRoot = false);
            map.AfterMapping((source, destination) => destination.HasSquareRoot = destination.SquareRoot != null);

            Entity1 entity;
            Model1 model1 = new Model1() { ID = 1, Name = "MyName", DateTime = DateTime.Now, Number = 25 };
            entity = MapFactory.Map<Model1, Entity1>(model1);
            entity.Should().NotBeNull();
            entity.ID.Should().Be(1);
            entity.Title.Should().Be("MyName");
            entity.DateTime.Should().Be(model1.DateTime);
            entity.Number.Should().Be(25);
            entity.SquareRoot.Should().Be(5);
            entity.HasSquareRoot.Should().BeTrue();

            entity = new Entity1();
            MapFactory.Map<Model1, Entity1>(model1, entity);
            entity.ID.Should().Be(1);
            entity.Title.Should().Be("MyName");
            entity.DateTime.Should().Be(model1.DateTime);
            entity.Number.Should().Be(25);
            entity.SquareRoot.Should().Be(5);
            entity.HasSquareRoot.Should().BeTrue();

            Model1[] models = new Model1[]
            {
                new Model1() {ID = 3, Name = "MyName1", DateTime = DateTime.Now, Number = 9},
                new Model1() {ID = 9, Name = "MyName2", DateTime = DateTime.Now, Number = 81},
                null,
            };

            Entity1[] entities = MapFactory.Map<Model1[], Entity1[]>(models);

            entities.Should().NotBeNull();
            entities.Length.Should().Be(3);
            entities[0].Should().NotBeNull();
            entities[1].Should().NotBeNull();
            entities[2].Should().BeNull();
            entities[0].SquareRoot.Should().Be(3);
            entities[1].SquareRoot.Should().Be(9);
        }
    }
}

