using System;
using System.Data;
using System.Reflection;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Mapper;
using AwesomeAssertions;
using Xunit;

namespace Gehtsoft.EF.Toolbox.Test
{
    /// <summary>
    /// Targeted coverage for <c>Gehtsoft.EF.Mapper</c> paths the scenario suite in
    /// <see cref="TestMappingNewMaps"/> does not reach: the EF-aware <see cref="EfMap{TSource, TDestination}"/>
    /// (constructor + <c>GetTargetByName</c> override, both the descriptor and the non-entity
    /// branches), the guard / skip / not-found branches of <see cref="EntityMapInitializer"/>
    /// (<c>SourceToModel</c> and <c>ModelToSource</c>), and the null-object guards of the two
    /// primary-key accessors. Types are local and the initializers are invoked directly against
    /// <c>new Map&lt;,&gt;()</c> instances so the tests do not pollute the static
    /// <see cref="MapFactory"/> cache shared with the other suites.
    /// </summary>
    public class TestEfMapperCoverage
    {
        [Entity(Scope = "efmapcov")]
        public class EfCovRefEntity
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        }

        [Entity(Scope = "efmapcov")]
        public class EfCovMainEntity
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(ForeignKey = true)]
            public EfCovRefEntity Ref { get; set; }

            [EntityProperty]
            public string Title { get; set; }

            // a column that the entity itself excludes from auto-mapping
            [EntityProperty]
            [DoNotAutoMap]
            public string Secret { get; set; }

            // a plain CLR property that is not a column at all
            public string NotAColumn { get; set; }
        }

        public class PlainClass
        {
            public string Anything { get; set; }
        }

        // ---------------------------------------------------------------- EfMap

        [Fact]
        public void EfMap_Constructor_Binds_Entity_Descriptor()
        {
            // TDestination is an entity -> the constructor resolves and stores its descriptor.
            var map = new EfMap<PlainClass, EfCovMainEntity>();
            map.Destination.Should().Be(typeof(EfCovMainEntity));
        }

        [Fact]
        public void EfMap_Resolves_Rule_By_Property_Name_And_By_Db_Column_Name()
        {
            var map = new EfMap<PlainClass, EfCovMainEntity>();
            map.For(nameof(EfCovMainEntity.Title)).From(s => s.Anything);

            TableDescriptor.ColumnInfo column = Column(nameof(EfCovMainEntity.Title));

            // GetTargetByName resolves a column ID or a DB column name to the CLR property, so the
            // rule registered by property name is discoverable through all three spellings.
            map.ContainsRuleFor(nameof(EfCovMainEntity.Title)).Should().BeTrue(); // CLR property name
            map.ContainsRuleFor(column.Name).Should().BeTrue();                    // DB column name
            map.ContainsRuleFor(column.ID).Should().BeTrue();                      // column ID

            // "NotAColumn" is a CLR property but not a column -> base resolution, no matching rule.
            map.ContainsRuleFor("NotAColumn").Should().BeFalse();
            // neither a column nor a property -> base returns null -> no match.
            map.ContainsRuleFor("DoesNotExistAnywhere").Should().BeFalse();
        }

        [Fact]
        public void EfMap_Without_Entity_Destination_Uses_Base_Resolution()
        {
            // TDestination is not an entity -> the constructor leaves the descriptor null and
            // GetTargetByName takes the null-descriptor branch straight to the base implementation.
            var map = new EfMap<EfCovMainEntity, PlainClass>();

            map.For("Anything").From(s => s.Title);
            map.ContainsRuleFor("Anything").Should().BeTrue();
        }

        // ------------------------------------------------- EntityMapInitializer

        [MapEntity(typeof(EfCovMainEntity))]
        public class ModelWithIgnoredProperty
        {
            [MapProperty]
            public int ID { get; set; }

            [DoNotAutoMap]
            [MapProperty]
            public string Ignored { get; set; }
        }

        [MapEntity(typeof(EfCovMainEntity))]
        public class ModelMappingSecret
        {
            [MapProperty(Name = nameof(EfCovMainEntity.Secret))]
            public string Secret { get; set; }
        }

        [MapEntity(typeof(EfCovMainEntity))]
        public class ModelWithBadName
        {
            [MapProperty(Name = "NoSuchColumnOrProperty")]
            public string Value { get; set; }
        }

        // deliberately carries no [MapEntity] attribute
        public class ModelWithNoMappingAttribute
        {
            [MapProperty]
            public int ID { get; set; }
        }

        [Fact]
        public void SourceToModel_Throws_When_Model_Has_No_Mapping_Attribute()
        {
            var map = new Map<EfCovMainEntity, ModelWithNoMappingAttribute>();
            ((Action)(() => new EntityMapInitializer().SourceToModel(map)))
                .Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ModelToSource_Throws_When_Model_Has_No_Mapping_Attribute()
        {
            var map = new Map<ModelWithNoMappingAttribute, EfCovMainEntity>();
            ((Action)(() => new EntityMapInitializer().ModelToSource(map)))
                .Should().Throw<ArgumentException>();
        }

        [Fact]
        public void SourceToModel_Throws_When_Entity_Type_Does_Not_Match()
        {
            // destination model targets EfCovMainEntity, but the map source is EfCovRefEntity
            var map = new Map<EfCovRefEntity, ModelWithIgnoredProperty>();
            ((Action)(() => new EntityMapInitializer().SourceToModel(map)))
                .Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ModelToSource_Throws_When_Entity_Type_Does_Not_Match()
        {
            var map = new Map<ModelWithIgnoredProperty, EfCovRefEntity>();
            ((Action)(() => new EntityMapInitializer().ModelToSource(map)))
                .Should().Throw<ArgumentException>();
        }

        [Fact]
        public void SourceToModel_Skips_DoNotAutoMap_Model_Property_And_DoNotAutoMap_Column()
        {
            var map1 = new Map<EfCovMainEntity, ModelWithIgnoredProperty>();
            new EntityMapInitializer().SourceToModel(map1);
            map1.ContainsRuleFor(nameof(ModelWithIgnoredProperty.ID)).Should().BeTrue();
            map1.ContainsRuleFor(nameof(ModelWithIgnoredProperty.Ignored)).Should().BeFalse();

            // the column itself is marked [DoNotAutoMap] on the entity -> skipped
            var map2 = new Map<EfCovMainEntity, ModelMappingSecret>();
            new EntityMapInitializer().SourceToModel(map2);
            map2.ContainsRuleFor(nameof(ModelMappingSecret.Secret)).Should().BeFalse();
        }

        [Fact]
        public void SourceToModel_Throws_When_Name_Is_Neither_Column_Nor_Property()
        {
            var map = new Map<EfCovMainEntity, ModelWithBadName>();
            ((Action)(() => new EntityMapInitializer().SourceToModel(map)))
                .Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void ModelToSource_Skips_DoNotAutoMap_Model_Property_And_DoNotAutoMap_Column()
        {
            var map1 = new Map<ModelWithIgnoredProperty, EfCovMainEntity>();
            new EntityMapInitializer().ModelToSource(map1);
            map1.Mappings.Count.Should().Be(1);   // ID mapped; Ignored skipped via [DoNotAutoMap]
            // ModelToSource now uses ClassPropertyAccessor targets, so rules are discoverable by name.
            map1.ContainsRuleFor(nameof(EfCovMainEntity.ID)).Should().BeTrue();

            var map2 = new Map<ModelMappingSecret, EfCovMainEntity>();
            new EntityMapInitializer().ModelToSource(map2);
            map2.Mappings.Count.Should().Be(0);   // Secret column is [DoNotAutoMap] on the entity
            map2.ContainsRuleFor(nameof(EfCovMainEntity.Secret)).Should().BeFalse();
        }

        [Fact]
        public void ModelToSource_Throws_When_Name_Is_Neither_Column_Nor_Property()
        {
            var map = new Map<ModelWithBadName, EfCovMainEntity>();
            ((Action)(() => new EntityMapInitializer().ModelToSource(map)))
                .Should().Throw<InvalidOperationException>();
        }

        // ----------------------------------------------- primary-key accessors

        private static TableDescriptor.ColumnInfo Column(string propertyName)
        {
            EntityDescriptor descriptor = AllEntities.Inst[typeof(EfCovMainEntity)];
            for (int i = 0; i < descriptor.TableDescriptor.Count; i++)
                if (descriptor.TableDescriptor[i].PropertyAccessor.Name == propertyName)
                    return descriptor.TableDescriptor[i];
            throw new InvalidOperationException("column not found: " + propertyName);
        }

        private static TableDescriptor.ColumnInfo ForeignKeyColumn()
        {
            EntityDescriptor descriptor = AllEntities.Inst[typeof(EfCovMainEntity)];
            for (int i = 0; i < descriptor.TableDescriptor.Count; i++)
                if (descriptor.TableDescriptor[i].ForeignKey)
                    return descriptor.TableDescriptor[i];
            throw new InvalidOperationException("foreign key column not found");
        }

        [Fact]
        public void EntityPrimaryKeySource_Get_Null_Returns_Null()
        {
            var source = new EntityPrimaryKeySource(ForeignKeyColumn());
            source.Get(null).Should().BeNull();
        }

        [Fact]
        public void ModelPrimaryKeySource_Get_Null_Returns_Null()
        {
            PropertyInfo anyProperty = typeof(EfCovMainEntity).GetProperty(nameof(EfCovMainEntity.ID));
            var source = new ModelPrimaryKeySource(ForeignKeyColumn(), anyProperty);
            source.Get(null).Should().BeNull();
        }
    }
}
