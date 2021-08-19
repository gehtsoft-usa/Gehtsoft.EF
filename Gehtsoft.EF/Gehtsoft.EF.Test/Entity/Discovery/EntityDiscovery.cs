using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using SharpCompress.Common;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Discovery
{
    public class Dicoverer
    {
        [Entity(Scope = "tablebuilder", Table = "t1", Metadata = typeof(ExactSpecMetadata))]
        public class ExactSpec
        {
            [EntityProperty(Field = "f1", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int IntProperty { get; set; }

            [EntityProperty(Field = "f2", DbType = DbType.Int64, Nullable = true)]
            public long? LongProperty { get; set; }

            [EntityProperty(Field = "f3", DbType = DbType.Boolean, Nullable = false)]
            public bool BoolProperty { get; set; }

            [EntityProperty(Field = "f4", DbType = DbType.Double, Precision = 4, Sorted = true)]
            public double DoubleProperty { get; set; }

            [EntityProperty(Field = "f5", DbType = DbType.Decimal, Precision = 2, Unique = true)]
            public decimal DecimalProperty { get; set; }

            [EntityProperty(Field = "f6", DbType = DbType.String, Size = 128)]
            public string StringProperty { get; set; }

            [EntityProperty(Field = "f7", DbType = DbType.Date)]
            public DateTime DateProperty { get; set; }

            [EntityProperty(Field = "f8", DbType = DbType.DateTime)]
            public DateTime TimestampProperty { get; set; }

            [EntityProperty(Field = "f9", DbType = DbType.Guid)]
            public Guid GuidProperty { get; set; }

            [EntityProperty(Field = "f10", DbType = DbType.Binary, Size = 256, Nullable = true)]
            public byte[] Blob { get; set; }
        }

        public class ExactSpecMetadata
        {
        }

        [Entity(Scope = "tablebuilder")]
        public class Dict1
        {
            [AutoId]
            public int ID { get; set; }
        }

        [Entity(Scope = "tablebuilder")]
        public class Dict2
        {
            [EntityProperty(PrimaryKey = true)]
            public Guid ID { get; set; }
        }

        [Entity(Scope = "tablebuilder")]
        public class DefaultSpec
        {
            [AutoId]
            public int IntProperty { get; set; }

            [EntityProperty]
            public long? LongProperty { get; set; }

            [EntityProperty]
            public bool BoolProperty { get; set; }

            [EntityProperty]
            public double DoubleProperty { get; set; }

            [EntityProperty]
            public decimal DecimalProperty { get; set; }

            [EntityProperty]
            public string StringProperty { get; set; }

            [EntityProperty]
            public DateTime DateProperty { get; set; }

            [EntityProperty]
            public byte[] Blob { get; set; }

            [ForeignKey]
            public Dict1 Reference1 { get; set; }

            [ForeignKey(Nullable = true)]
            public Dict2 Reference2 { get; set; }
        }

        [ObsoleteEntity(Scope = "tablebuilder")]
        public class ObsoleteEntity
        {
            [AutoId]
            public int ID { get; set; }
        }

        public class DynamicTestEntity : DynamicEntity
        {
            public override EntityAttribute EntityAttribute
            {
                get
                {
                    return new EntityAttribute()
                    {
                        Scope = "tablebuilder",
                        Table = "dynamictable",
                    };
                }
            }

            protected override IEnumerable<IDynamicEntityProperty> InitializeProperties()
            {
                yield return new DynamicEntityProperty(typeof(int), "Property1", new EntityPropertyAttribute()
                {
                    PrimaryKey = true,
                    Autoincrement = true,
                });

                yield return new DynamicEntityProperty(typeof(string), "Property2", new EntityPropertyAttribute()
                {
                    Sorted = true,
                    Nullable = true,
                    Size = 84,
                });
            }
        }

        [Theory]
        [InlineData(typeof(ExactSpec), "t1", 10)]
        [InlineData(typeof(DefaultSpec), "DefaultSpec", 10)]
        [InlineData(typeof(ObsoleteEntity), "ObsoleteEntity", 1)]
        [InlineData(typeof(DynamicTestEntity), "dynamictable", 2)]
        public void TableSpec(Type entityType, string tableName, int fieldCount)
        {
            var entityInfo = AllEntities.Inst[entityType];
            var table = entityInfo.TableDescriptor;

            table.Name.Should().Be(tableName);
            table.Count.Should().Be(fieldCount);
        }

        [Theory]
        [InlineData(typeof(ExactSpec), 0, "f1", DbType.Int32, true, true, false, false, false, false, 0, 0)]
        [InlineData(typeof(ExactSpec), 1, "f2", DbType.Int64, false, false, true, false, false, false, 0, 0)]
        [InlineData(typeof(ExactSpec), 2, "f3", DbType.Boolean, false, false, false, false, false, false, 0, 0)]
        [InlineData(typeof(ExactSpec), 3, "f4", DbType.Double, false, false, false, true, false, false, 0, 4)]
        [InlineData(typeof(ExactSpec), 4, "f5", DbType.Decimal, false, false, false, false, true, false, 0, 2)]
        [InlineData(typeof(ExactSpec), 5, "f6", DbType.String, false, false, false, false, false, false, 128, 0)]
        [InlineData(typeof(ExactSpec), 6, "f7", DbType.Date, false, false, false, false, false, false, 0, 0)]
        [InlineData(typeof(ExactSpec), 7, "f8", DbType.DateTime, false, false, false, false, false, false, 0, 0)]
        [InlineData(typeof(ExactSpec), 8, "f9", DbType.Guid, false, false, false, false, false, false, 0, 0)]
        [InlineData(typeof(ExactSpec), 9, "f10", DbType.Binary, false, false, true, false, false, false, 256, 0)]

        [InlineData(typeof(DefaultSpec), 0, "intproperty", DbType.Int32, true, true, false, false, false, false, 0, 0)]
        [InlineData(typeof(DefaultSpec), 1, "longproperty", DbType.Int64, false, false, true, false, false, false, 0, 0)]
        [InlineData(typeof(DefaultSpec), 2, "boolproperty", DbType.Boolean, false, false, false, false, false, false, 0, 0)]
        [InlineData(typeof(DefaultSpec), 3, "doubleproperty", DbType.Double, false, false, false, false, false, false, 18, 7)]
        [InlineData(typeof(DefaultSpec), 4, "decimalproperty", DbType.Decimal, false, false, false, false, false, false, 18, 4)]
        [InlineData(typeof(DefaultSpec), 5, "stringproperty", DbType.String, false, false, false, false, false, false, 0, 0)]
        [InlineData(typeof(DefaultSpec), 6, "dateproperty", DbType.DateTime, false, false, false, false, false, false, 0, 0)]
        [InlineData(typeof(DefaultSpec), 7, "blob", DbType.Binary, false, false, true, false, false, false, 0, 0)]
        [InlineData(typeof(DefaultSpec), 8, "reference1", DbType.Int32, false, false, false, false, false, true, 0, 0)]
        [InlineData(typeof(DefaultSpec), 9, "reference2", DbType.Guid, false, false, true, false, false, true, 0, 0)]

        [InlineData(typeof(DynamicTestEntity), 0, "property1", DbType.Int32, true, true, false, false, false, false, 0, 0)]
        [InlineData(typeof(DynamicTestEntity), 1, "property2", DbType.String, false, false, true, true, false, false, 84, 0)]
        public void Field(Type entityType, int index, string name, DbType fieldType, bool pk, bool auto, bool nullable, bool sorted, bool unique, bool fk, int size, int precision)
        {
            var entityInfo = AllEntities.Inst[entityType];
            var table = entityInfo.TableDescriptor;
            var column = table[index];

            column.Name.Should().Be(name);
            column.DbType.Should().Be(fieldType);
            column.PrimaryKey.Should().Be(pk);
            column.ForeignKey.Should().Be(fk);
            column.Autoincrement.Should().Be(auto);
            column.Nullable.Should().Be(nullable);
            column.Sorted.Should().Be(sorted);
            column.Unique.Should().Be(unique);
            column.Size.Should().Be(size);
            column.Precision.Should().Be(precision);
        }

        [Theory]
        [InlineData(nameof(ExactSpec.IntProperty), typeof(int), 0, 123)]
        [InlineData(nameof(ExactSpec.LongProperty), typeof(long?), null, 456)]
        [InlineData(nameof(ExactSpec.LongProperty), typeof(long?), null, null)]
        [InlineData(nameof(ExactSpec.BoolProperty), typeof(bool), false, true)]
        [InlineData(nameof(ExactSpec.DoubleProperty), typeof(double), 0, 1.2345)]
        [InlineData(nameof(ExactSpec.DecimalProperty), typeof(decimal), 0, 1.23)]
        [InlineData(nameof(ExactSpec.StringProperty), typeof(string), null, "1234")]
        [InlineData(nameof(ExactSpec.DateProperty), typeof(DateTime), 0, "2010-05-22")]
        [InlineData(nameof(ExactSpec.TimestampProperty), typeof(DateTime), 0, "2010-05-22 23:15:47")]
        [InlineData(nameof(ExactSpec.Blob), typeof(byte[]), null, "0123456789abcd")]
        public void PropertyAccessor(string field, Type valueType, object defaultValue, object testValue)
        {
            var entityInfo = AllEntities.Inst[typeof(ExactSpec)];
            var table = entityInfo.TableDescriptor;
            var entity = new ExactSpec();

            defaultValue = TestValue.Translate(valueType, defaultValue);
            testValue = TestValue.Translate(valueType, testValue);

            table[field].PropertyAccessor.PropertyType.Should().Be(valueType);
            table[field].PropertyAccessor.GetValue(entity).Should().Be(defaultValue);
            table[field].PropertyAccessor.SetValue(entity, testValue);
            table[field].PropertyAccessor.GetValue(entity).Should().Be(testValue);
        }

        [Theory]
        [InlineData("Property1", typeof(int), 0, 123)]
        [InlineData("Property2", typeof(string), null, "1234")]
        public void DynamicPropertyAccessor(string field, Type valueType, object defaultValue, object testValue)
        {
            var entityInfo = AllEntities.Inst[typeof(DynamicTestEntity)];
            var table = entityInfo.TableDescriptor;
            var entity = new DynamicTestEntity();

            defaultValue = TestValue.Translate(valueType, defaultValue);
            testValue = TestValue.Translate(valueType, testValue);

            table[field].PropertyAccessor.PropertyType.Should().Be(valueType);
            table[field].PropertyAccessor.GetValue(entity).Should().Be(defaultValue);
            table[field].PropertyAccessor.SetValue(entity, testValue);
            table[field].PropertyAccessor.GetValue(entity).Should().Be(testValue);
        }

        [Fact]
        public void ForgetType()
        {
            AllEntities.Inst[typeof(ExactSpec)].Should().NotBeNull();
            AllEntities.Inst[typeof(DefaultSpec)].Should().NotBeNull();
            AllEntities.Inst.Should().Contain(e => e == typeof(ExactSpec));
            AllEntities.Inst.Should().Contain(e => e == typeof(DefaultSpec));
            AllEntities.Inst.ForgetType(typeof(ExactSpec));
            AllEntities.Inst.Should().NotContain(e => e == typeof(ExactSpec));
            AllEntities.Inst.Should().Contain(e => e == typeof(DefaultSpec));
        }

        [Fact]
        public void ForgetScope()
        {
            AllEntities.Get<ExactSpec>().Should().NotBeNull();
            AllEntities.Get<DefaultSpec>().Should().NotBeNull();
            AllEntities.Inst.Should().Contain(e => e == typeof(ExactSpec));
            AllEntities.Inst.Should().Contain(e => e == typeof(DefaultSpec));
            AllEntities.Inst.ForgetScope("tablebuilder");
            AllEntities.Inst.Should().NotContain(e => e == typeof(ExactSpec));
            AllEntities.Inst.Should().NotContain(e => e == typeof(DefaultSpec));
        }

        [Fact]
        public void DiscoveryEvents()
        {
            var signalled = new List<Tuple<object, EntityDescriptor>>();
            var action = new EventHandler<EntityDescriptorEventArgs>((o, args) =>
                signalled.Add(new Tuple<object, EntityDescriptor>(o, args?.Entity)));

            AllEntities.Inst.OnEntityDiscovered += action;
            using (var unsubscribe = new DelayedAction(() => AllEntities.Inst.OnEntityDiscovered -= action))
            {
                AllEntities.Inst.ForgetScope("tablebuilder");
                AllEntities.Get<ExactSpec>().Should().NotBeNull();

                signalled.Should().Contain(t => t.Item1 == AllEntities.Inst && t.Item2.EntityType == typeof(ExactSpec));
                signalled.Should().NotContain(t => t.Item1 == AllEntities.Inst && t.Item2.EntityType == typeof(DefaultSpec));

                AllEntities.Get<DefaultSpec>().Should().NotBeNull();

                signalled.Should().Contain(t => t.Item1 == AllEntities.Inst && t.Item2.EntityType == typeof(DefaultSpec));
            }

            AllEntities.Inst.ForgetScope("tablebuilder");
            var currentCount = signalled.Count;
            AllEntities.Inst[typeof(ExactSpec)].Should().NotBeNull();
            signalled.Should().HaveCount(currentCount);
        }

        [Fact]
        public void Metadata()
        {
            AllEntities.Get<ExactSpec>().TableDescriptor.Metadata.Should().NotBeNull();
            AllEntities.Get<ExactSpec>().TableDescriptor.Metadata.Should().BeOfType<ExactSpecMetadata>();

            AllEntities.Get<DefaultSpec>().TableDescriptor.Metadata.Should().BeNull();
        }

        [Fact]
        public void Exception()
        {
            AllEntities.Get<ExactSpecMetadata>(false).Should().BeNull();
            ((Action)(() => AllEntities.Get<ExactSpecMetadata>(true))).Should().Throw<EfSqlException>();
            ((Action)(() => AllEntities.Get<ExactSpecMetadata>())).Should().Throw<EfSqlException>();
        }
    }
}

