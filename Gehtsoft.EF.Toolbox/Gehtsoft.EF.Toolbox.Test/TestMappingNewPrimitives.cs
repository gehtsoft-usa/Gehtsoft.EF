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
using Gehtsoft.Tools.TypeUtils;
using NUnit.Framework;

namespace Gehtsoft.EF.Toolbox.Test
{
    [TestFixture]
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

        class Model2
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

        [Test]
        public void TestTypeRecognizer()
        {
            Assert.AreEqual(null, ValueMapper.GetElementType(typeof(int)));
            Assert.AreEqual(typeof(char), ValueMapper.GetElementType(typeof(string)));
            Assert.AreEqual(typeof(string), ValueMapper.GetElementType(typeof(EntityCollection<string>)));
            Assert.AreEqual(typeof(double?), ValueMapper.GetElementType(typeof(List<double?>)));
            Assert.AreEqual(typeof(DateTime?), ValueMapper.GetElementType(typeof(DateTime?[])));
            Assert.AreEqual(typeof(string[]), ValueMapper.GetElementType(typeof(EntityCollection<string[]>)));
            Assert.AreEqual(typeof(List), ValueMapper.GetElementType(typeof(Queue<List>)));
        }

        [Test]
        public void TestProperty()
        {
            Model1 model1 = new Model1();

            ClassPropertyAccessor mapping = new ClassPropertyAccessor(typeof(Model1).GetTypeInfo().GetProperty(nameof(Model1.ID)));
            Assert.AreEqual(nameof(Model1.ID), mapping.Name);
            Assert.AreEqual(typeof(int), mapping.ValueType);
            mapping.Set(model1, 10);
            Assert.AreEqual(10, model1.ID);
            model1.ID = 15;
            Assert.AreEqual(15, mapping.Get(model1));

            ClassPropertyAccessor mapping1 = null;
            Assert.IsFalse(mapping.Equals(mapping1));
            mapping1 = mapping;
            Assert.IsTrue(mapping.Equals(mapping1));
            mapping1 = new ClassPropertyAccessor(typeof(Model1).GetTypeInfo().GetProperty(nameof(Model1.ID)));
            Assert.IsTrue(mapping.Equals(mapping1));
            Assert.IsFalse(ReferenceEquals(mapping, mapping1));
            mapping1 = new ClassPropertyAccessor(typeof(Model1).GetTypeInfo().GetProperty(nameof(Model1.Name)));
            Assert.IsFalse(mapping.Equals(mapping1));

        }

        [Test]
        public void TestEntityProperty()
        {
            Entity1 entity1 = new Entity1();
            EntityDescriptor entity1Descriptor = AllEntities.Inst[typeof(Entity1)];

            EntityPropertyAccessor accessor = new EntityPropertyAccessor(entity1Descriptor[nameof(Entity1.ID)]);
            Assert.AreEqual(nameof(Entity1.ID), accessor.Name);
            Assert.AreEqual(typeof(int), accessor.ValueType);
            accessor.Set(entity1, 10);
            Assert.AreEqual(10, entity1.ID);
            entity1.ID = 15;
            Assert.AreEqual(15, accessor.Get(entity1));

            EntityPropertyAccessor mapping1 = null;
            Assert.IsFalse(accessor.Equals(mapping1));
            mapping1 = accessor;
            Assert.IsTrue(accessor.Equals(mapping1));
            mapping1 = new EntityPropertyAccessor(entity1Descriptor[nameof(Entity1.ID)]);
            Assert.IsTrue(accessor.Equals(mapping1));
            Assert.IsFalse(ReferenceEquals(accessor, mapping1));
            mapping1 = new EntityPropertyAccessor(entity1Descriptor[nameof(Entity1.Title)]);
            Assert.IsFalse(accessor.Equals(mapping1));


            Entity2 entity2 = new Entity2();
            EntityDescriptor entity2Descriptor = AllEntities.Inst[typeof(Entity2)];
            EntityPrimaryKeySource pksource = new EntityPrimaryKeySource(entity2Descriptor[nameof(Entity2.Reference)]);
            Assert.AreEqual(nameof(Entity2.Reference), pksource.Name);
            Assert.AreEqual(typeof(int), pksource.ValueType);
            Assert.IsNull(pksource.Get(entity2));

            entity2.Reference = entity1;
            Assert.AreEqual(15, pksource.Get(entity2));

            Model2 model2 = new Model2();
            ModelPrimaryKeySource pksource1 = new ModelPrimaryKeySource(entity2Descriptor[nameof(Entity2.Reference)], model2.GetType().GetTypeInfo().GetProperty(nameof(Model2.Reference)));
            Assert.AreEqual(nameof(Model2.Reference), pksource1.Name);
            Assert.AreEqual(typeof(Entity1), pksource1.ValueType);
            Assert.IsNull(pksource1.Get(model2));
            model2.Reference = 55;
            object ro = pksource1.Get(model2);
            Assert.IsNotNull(ro);
            Assert.AreEqual(typeof(Entity1), ro.GetType());
            Assert.AreEqual(55, (ro as Entity1).ID);
        }

        [Test]
        public void TestFunctionSource()
        {
            Model1 model1 = new Model1() {ID = 10, Number = null};

            ExpressionSource<Model1, double> expression = new ExpressionSource<Model1, double>(model => (double) model.ID + (model.Number ?? 5));
            Assert.AreEqual(typeof(double), expression.ValueType);
            Assert.AreEqual("expression", expression.Name);
            Assert.AreEqual(10, model1.ID);
            Assert.AreEqual(15, expression.Get(model1));

            model1.Number = 55;
            Assert.AreEqual(65, expression.Get(model1));
        }

        public enum TestEnum
        {
            E1 = 10,
            E2 = 11,
            E3 = 12,
        }

        [Test]
        public void TestValueMapper()
        {
            //test null mappings
            Assert.IsNull(ValueMapper.MapValue(null, typeof(Model1)));
            Assert.IsNull(ValueMapper.MapValue(null, typeof(int?)));
            Assert.AreEqual(0, ValueMapper.MapValue(null, typeof(int)));
            Assert.AreEqual(new DateTime(0), ValueMapper.MapValue(null, typeof(DateTime)));

            //test self-mappings
            Model1 model1 = new Model1();
            Assert.IsTrue(ReferenceEquals(model1, ValueMapper.MapValue(model1, typeof(Model1))));
            Assert.AreEqual(10, ValueMapper.MapValue(10, typeof(int)));

            //test value-to-value mappings
            Assert.AreEqual("10", ValueMapper.MapValue(10, typeof(string)));
            Assert.IsTrue(ValueMapper.MapValue(10, typeof(string)) is string);
            Assert.AreEqual(10.0, ValueMapper.MapValue(10, typeof(double)));
            Assert.IsTrue(ValueMapper.MapValue(10, typeof(double)) is double);
            
            //test map-to-nullable mappings
            Assert.AreEqual(10.0, ValueMapper.MapValue(10, typeof(double?)));
            Assert.IsTrue(ValueMapper.MapValue(10, typeof(double?)) is double);

            //test enum mappings
            Assert.AreEqual(TestEnum.E1, ValueMapper.MapValue((int) TestEnum.E1, typeof(TestEnum)));
            Assert.AreEqual(TestEnum.E2, ValueMapper.MapValue(nameof(TestEnum.E2), typeof(TestEnum)));
            Assert.AreEqual(TestEnum.E3, ValueMapper.MapValue(TestEnum.E3, typeof(TestEnum)));
            Assert.AreEqual(TestEnum.E1, ValueMapper.MapValue((int) TestEnum.E1, typeof(TestEnum?)));
            Assert.AreEqual(TestEnum.E2, ValueMapper.MapValue(nameof(TestEnum.E2), typeof(TestEnum?)));
            Assert.AreEqual(TestEnum.E3, ValueMapper.MapValue(TestEnum.E3, typeof(TestEnum?)));
            Assert.AreEqual(null, ValueMapper.MapValue(null, typeof(TestEnum?)));

            //array-to-array mappings
            int[] array1 = new []{1, 2, 3, 4, 5};
            double[] array2;
            object ro;


            ro = ValueMapper.MapValue(array1, typeof(double[]));
            Assert.IsNotNull(ro);
            Assert.IsTrue(ro is double[]);
            array2 = (double[]) ro;
            Assert.AreEqual(new double[] {1, 2, 3, 4, 5}, array2);

            //enumerable-to-array mappings
            List<string> list = new List<string>(new string[] {"1.1", "2.2", "3.3"});
            ro = ValueMapper.MapValue(list, typeof(double[]));
            Assert.IsNotNull(ro);
            Assert.IsTrue(ro is double[]);
            array2 = (double[]) ro;
            Assert.AreEqual(new double[] {1.1, 2.2, 3.3}, array2);
            
            //array-to-list mappings
            ro = ValueMapper.MapValue(array1, typeof(List<string>));
            Assert.IsNotNull(ro);
            Assert.IsTrue(ro is List<string>);
            list = (List<string>) ro;
            Assert.AreEqual(new string[] {"1", "2", "3", "4", "5"}, list);

            //enumerable-to-list mappings
            Queue<DateTime> queue = new Queue<DateTime>();
            queue.Enqueue(new DateTime(2000, 1, 1));
            queue.Enqueue(new DateTime(2001, 1, 1));
            queue.Enqueue(new DateTime(2002, 1, 1));
            ro = ValueMapper.MapValue(queue, typeof(List<string>));
            Assert.IsNotNull(ro);
            Assert.IsTrue(ro is List<string>);
            list = (List<string>) ro;
            Assert.AreEqual(new string[] {new DateTime(2000, 1, 1).ToString(CultureInfo.InvariantCulture), new DateTime(2001, 1, 1).ToString(CultureInfo.InvariantCulture), new DateTime(2002, 1, 1).ToString(CultureInfo.InvariantCulture)}, list);

            //flags
            Assert.AreEqual("abcd", ValueMapper.MapValue(" abcd ", typeof(string), MapFlag.TrimStrings));
            Assert.AreEqual(0, ((DateTime) ValueMapper.MapValue(DateTime.Now, typeof(DateTime), MapFlag.TrimToSeconds)).Millisecond);
            Assert.AreEqual(0, ((DateTime) ValueMapper.MapValue(DateTime.Now, typeof(DateTime), MapFlag.TrimToDate)).Hour);
            Assert.AreEqual(0, ((DateTime) ValueMapper.MapValue(DateTime.Now, typeof(DateTime), MapFlag.TrimToDate)).Minute);
            Assert.AreEqual(0, ((DateTime) ValueMapper.MapValue(DateTime.Now, typeof(DateTime), MapFlag.TrimToDate)).Second);
        }

        [Test]
        public void PropertyMappingTest()
        {
            PropertyMapping<Model1, Entity1> propertyMapping = new PropertyMapping<Model1, Entity1>(null);
            propertyMapping.From(s => s.ID).To(d => d.ID).When(s => s.ID > 10);
            Assert.IsNotNull(propertyMapping.Source);
            Assert.IsTrue(propertyMapping.Source is ClassPropertyAccessor);
            Assert.IsNotNull(propertyMapping.Target);
            Assert.IsTrue(propertyMapping.Target is ClassPropertyAccessor);

            Model1 model = new Model1();
            Entity1 entity = new Entity1();

            model.ID = 20;
            propertyMapping.Map(model, entity);
            Assert.AreEqual(20, entity.ID);
            model.ID = 10;
            propertyMapping.Map(model, entity);
            Assert.AreEqual(20, entity.ID);

            propertyMapping.Ignore();
            model.ID = 25;
            propertyMapping.Map(model, entity);
            Assert.AreEqual(20, entity.ID);

            propertyMapping.Always();
            model.ID = 10;
            propertyMapping.Map(model, entity);
            Assert.AreEqual(10, entity.ID);

            Assert.Throws<ArgumentException>(() => propertyMapping.From(s => s.ID + 0x1000).To(d => d.ID + 50));
            Assert.Throws<ArgumentException>(() => propertyMapping.From(s => s.ID + 0x1000).To("123"));
            propertyMapping.From(s => s.ID + 0x1000).To(nameof(Entity1.ID)).Always();
            model.ID = 1;
            propertyMapping.Map(model, entity);
            Assert.AreEqual(0x1001, entity.ID);
        }

        [Test]
        public void ManuallyCreatedMapTest()
        {
            Map<Model1, Entity1> map = new Map<Model1, Entity1>();

            map.For(d => d.ID).From(s => s.ID);
            map.For(d => d.Title).From(nameof(Model1.Name));
            map.For(nameof(Entity1.DateTime)).From(s => s.DateTime);
            map.For(d => d.Number).From(s => s.Number);
            map.For(d => d.SquareRoot).From(s => Math.Sqrt(s.Number ?? 0)).When(s => s.Number != null);

            Model1 model1 = new Model1() {ID = 1, Name = "MyName", DateTime = DateTime.Now, Number = 25};
            Entity1 entity = map.Do(model1);

            Assert.IsNotNull(entity);
            Assert.AreEqual(1, entity.ID);
            Assert.AreEqual("MyName", entity.Title);
            Assert.AreEqual(model1.DateTime, entity.DateTime);
            Assert.AreEqual(25, entity.Number);
            Assert.AreEqual(5, entity.SquareRoot);

            map.Find(d => d.SquareRoot).ForEach(r => r.Ignore());
            entity = map.Do(model1);
            Assert.AreEqual(25, entity.Number);
            Assert.AreEqual(null, entity.SquareRoot);

            map.Find(d => d.SquareRoot).ForEach(r => r.WhenDestination(d => d.HasSquareRoot != null));
            entity = map.Do(model1);
            Assert.AreEqual(25, entity.Number);
            Assert.AreEqual(null, entity.SquareRoot);

            map.BeforeMapping((source, destination) => destination.HasSquareRoot = false);
            entity = map.Do(model1);
            Assert.AreEqual(25, entity.Number);
            Assert.AreEqual(5, entity.SquareRoot);
            Assert.IsFalse(entity.HasSquareRoot);


            map.AfterMapping((source, destination) => destination.HasSquareRoot = destination.SquareRoot != null);
            entity = map.Do(model1);
            Assert.IsTrue(entity.HasSquareRoot);

        }

        [Test]
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
            Model1 model1 = new Model1() {ID = 1, Name = "MyName", DateTime = DateTime.Now, Number = 25};
            entity = MapFactory.Map<Model1, Entity1>(model1);           
            Assert.IsNotNull(entity);
            Assert.AreEqual(1, entity.ID);
            Assert.AreEqual("MyName", entity.Title);
            Assert.AreEqual(model1.DateTime, entity.DateTime);
            Assert.AreEqual(25, entity.Number);
            Assert.AreEqual(5, entity.SquareRoot);
            Assert.IsTrue(entity.HasSquareRoot);

            entity = new Entity1();
            MapFactory.Map<Model1, Entity1>(model1, entity);
            Assert.AreEqual(1, entity.ID);
            Assert.AreEqual("MyName", entity.Title);
            Assert.AreEqual(model1.DateTime, entity.DateTime);
            Assert.AreEqual(25, entity.Number);
            Assert.AreEqual(5, entity.SquareRoot);
            Assert.IsTrue(entity.HasSquareRoot);

            Model1[] models = new Model1[]
            {
                new Model1() {ID = 3, Name = "MyName1", DateTime = DateTime.Now, Number = 9},
                new Model1() {ID = 9, Name = "MyName2", DateTime = DateTime.Now, Number = 81},
                null,
            };
            
            Entity1[] entities = MapFactory.Map<Model1[], Entity1[]>(models);

            Assert.IsNotNull(entities);
            Assert.AreEqual(3, entities.Length);
            Assert.IsNotNull(entities[0]);
            Assert.IsNotNull(entities[1]);
            Assert.IsNull(entities[2]);
            Assert.AreEqual(3, entities[0].SquareRoot);
            Assert.AreEqual(9, entities[1].SquareRoot);

        }

    }
}

