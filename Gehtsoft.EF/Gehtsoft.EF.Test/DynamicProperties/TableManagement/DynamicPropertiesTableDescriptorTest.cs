using System.Data;
using System.Linq;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.TableManagement
{
    public class DynamicPropertiesTableDescriptorTest
    {
        [Entity(Scope = "dynprops_eav")]
        public class PlainEntity
        {
            [AutoId]
            public int Id { get; set; }
        }

        [Entity(Scope = "dynprops_eav", Table = "owner_default")]
        [DynamicProperties]
        public class DefaultOwner : IDynamicPropertiesOwner
        {
            [AutoId]
            public int Id { get; set; }

            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        [Entity(Scope = "dynprops_eav", Table = "owner_custom")]
        [DynamicProperties(NameSize = 32, StringValueSize = 1024, RealValueSize = 20, RealValuePrecision = 6)]
        public class CustomOwner : IDynamicPropertiesOwner
        {
            [AutoId]
            public int Id { get; set; }

            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        [Entity(Scope = "dynprops_eav")]
        [DynamicProperties]
        public class NoPkOwner : IDynamicPropertiesOwner
        {
            [EntityProperty]
            public string Value { get; set; }

            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        [Fact]
        public void PlainEntity_HasNoEavTable()
        {
            AllEntities.Get<PlainEntity>().DynamicPropertiesTable.Should().BeNull();
        }

        [Fact]
        public void DefaultOwner_TableNameAndColumns()
        {
            TableDescriptor eav = AllEntities.Get<DefaultOwner>().DynamicPropertiesTable;

            eav.Should().NotBeNull();
            eav.Name.Should().Be("owner_default_props");

            eav.Count.Should().Be(7);

            eav["Id"].Name.Should().Be("id");
            eav["Id"].DbType.Should().Be(DbType.Int64);
            eav["Id"].PrimaryKey.Should().BeTrue();
            eav["Id"].Autoincrement.Should().BeTrue();
            eav["Id"].Nullable.Should().BeFalse();
            eav.PrimaryKey.Should().Be(eav["Id"]);

            eav["Name"].Name.Should().Be("name");
            eav["Name"].DbType.Should().Be(DbType.String);
            eav["Name"].Size.Should().Be(64);
            eav["Name"].Nullable.Should().BeFalse();

            eav["PropType"].Name.Should().Be("prop_type");
            eav["PropType"].DbType.Should().Be(DbType.Int32);
            eav["PropType"].Nullable.Should().BeFalse();

            eav["StringValue"].Name.Should().Be("v_str");
            eav["StringValue"].DbType.Should().Be(DbType.String);
            eav["StringValue"].Size.Should().Be(256);
            eav["StringValue"].Nullable.Should().BeTrue();

            eav["IntValue"].Name.Should().Be("v_int");
            eav["IntValue"].DbType.Should().Be(DbType.Int64);
            eav["IntValue"].Nullable.Should().BeTrue();

            eav["RealValue"].Name.Should().Be("v_real");
            eav["RealValue"].DbType.Should().Be(DbType.Double);
            eav["RealValue"].Size.Should().Be(0);
            eav["RealValue"].Precision.Should().Be(0);
            eav["RealValue"].Nullable.Should().BeTrue();
        }

        [Fact]
        public void DefaultOwner_OwnerColumnIsForeignKeyToOwner()
        {
            EntityDescriptor owner = AllEntities.Get<DefaultOwner>();
            TableDescriptor eav = owner.DynamicPropertiesTable;

            TableDescriptor.ColumnInfo ownerColumn = eav["Owner"];
            ownerColumn.Name.Should().Be("owner");
            ownerColumn.Nullable.Should().BeFalse();
            ownerColumn.ForeignKey.Should().BeTrue();
            ownerColumn.ForeignTable.Should().BeSameAs(owner.TableDescriptor);
            ownerColumn.DbType.Should().Be(owner.PrimaryKey.DbType);
        }

        [Fact]
        public void CustomOwner_HonoursAttributeOptions()
        {
            TableDescriptor eav = AllEntities.Get<CustomOwner>().DynamicPropertiesTable;

            eav.Name.Should().Be("owner_custom_props");
            eav["Name"].Size.Should().Be(32);
            eav["StringValue"].Size.Should().Be(1024);
            eav["RealValue"].Size.Should().Be(20);
            eav["RealValue"].Precision.Should().Be(6);
        }

        [Fact]
        public void CompositeIndexes_AreDefined()
        {
            TableDescriptor eav = AllEntities.Get<DefaultOwner>().DynamicPropertiesTable;

            eav.Metadata.Should().BeAssignableTo<ICompositeIndexMetadata>();
            var indexes = ((ICompositeIndexMetadata)eav.Metadata).Indexes.ToArray();

            indexes.Select(i => i.Name).Should()
                   .BeEquivalentTo(new[] { "owner_name", "name_str", "name_int", "name_real" });

            Names("owner_name").Should().Equal("owner", "name");
            Names("name_str").Should().Equal("name", "v_str");
            Names("name_int").Should().Equal("name", "v_int");
            Names("name_real").Should().Equal("name", "v_real");

            string[] Names(string indexName) =>
                indexes.First(i => i.Name == indexName).Fields.Select(f => f.Name).ToArray();
        }

        [Fact]
        public void DynamicPropertiesTable_IsCached()
        {
            EntityDescriptor owner = AllEntities.Get<DefaultOwner>();
            owner.DynamicPropertiesTable.Should().BeSameAs(owner.DynamicPropertiesTable);
        }

        [Fact]
        public void OwnerWithoutPrimaryKey_Throws()
        {
            EntityDescriptor owner = AllEntities.Get<NoPkOwner>();

            owner.Invoking(o => o.DynamicPropertiesTable)
                 .Should().Throw<EfSqlException>()
                 .Which.ErrorCode.Should().Be(EfExceptionCode.NoPrimaryKeyInTable);
        }
    }
}
