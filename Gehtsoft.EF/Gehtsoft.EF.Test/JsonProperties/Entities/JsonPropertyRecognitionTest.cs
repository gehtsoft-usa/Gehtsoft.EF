using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.JsonProperties.Entities
{
    public class JsonPropertyRecognitionTest
    {
        public class Profile
        {
            public int Age { get; set; }
            public string Name { get; set; }
        }

        [Entity(Scope = "jsonrecognition", Table = "json_owner")]
        public class JsonOwner
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 64, Nullable = true)]
            public string Name { get; set; }

            [JsonEntityProperty(Field = "profile", Nullable = true)]
            [JsonIndex("$.age", DbType.Int32)]
            [JsonIndex("$.address.zip", DbType.String, Unique = true)]
            [JsonIndex("$.name", DbType.String)]
            public Profile Data { get; set; }
        }

        [Entity(Scope = "jsonrecognition", Table = "json_plain")]
        public class JsonPlain
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            // a JSON property with default options and no declared indexes
            [JsonEntityProperty]
            public Profile Data { get; set; }
        }

        private static TableDescriptor.ColumnInfo Column(TableDescriptor td, string id)
        {
            foreach (TableDescriptor.ColumnInfo c in td)
                if (c.ID == id)
                    return c;
            return null;
        }

        [Fact]
        public void JsonColumn_IsRecognized_AsStringWithMetadata()
        {
            TableDescriptor td = AllEntities.Inst[typeof(JsonOwner)].TableDescriptor;

            var data = Column(td, "Data");
            data.Should().NotBeNull();
            data.Name.Should().Be("profile");
            data.DbType.Should().Be(DbType.String);
            data.Size.Should().Be(0, "a JSON document column is unbounded");
            data.Nullable.Should().BeTrue();
            data.ForeignKey.Should().BeFalse();

            data.Json.Should().NotBeNull();
            data.Json.ClrType.Should().Be(typeof(Profile));
        }

        [Fact]
        public void JsonIndexes_AreCollected_WithPathTypeUniqueAndAutoName()
        {
            TableDescriptor td = AllEntities.Inst[typeof(JsonOwner)].TableDescriptor;
            var indexes = Column(td, "Data").Json.Indexes;

            indexes.Should().HaveCount(3);

            // names are ALWAYS auto-derived from column + path + type (no override)
            indexes.Should().Contain(i => i.Path == "$.age" && i.DbType == DbType.Int32 && !i.Unique && i.Name == "profile_age_i32");
            indexes.Should().Contain(i => i.Path == "$.address.zip" && i.DbType == DbType.String && i.Unique && i.Name == "profile_address_zip_str");
            indexes.Should().Contain(i => i.Path == "$.name" && i.Name == "profile_name_str");
        }

        [Fact]
        public void JsonIndexName_EncodesType_SoTypeChangeChangesName()
        {
            TableDescriptor td = AllEntities.Inst[typeof(JsonOwner)].TableDescriptor;
            var indexes = Column(td, "Data").Json.Indexes;

            // the same path with a different declared type would yield a different index name,
            // which is what lets UpdateTables detect the change (drop old + create new).
            string ageName = null;
            foreach (var i in indexes)
                if (i.Path == "$.age")
                    ageName = i.Name;
            ageName.Should().Be("profile_age_i32");
            ageName.Should().NotBe("profile_age_str");
        }

        [Fact]
        public void RegularColumns_HaveNoJsonMetadata()
        {
            TableDescriptor td = AllEntities.Inst[typeof(JsonOwner)].TableDescriptor;
            Column(td, "ID").Json.Should().BeNull();
            Column(td, "Name").Json.Should().BeNull();
        }

        [Fact]
        public void JsonColumn_Defaults_NotNullableNoIndexes()
        {
            TableDescriptor td = AllEntities.Inst[typeof(JsonPlain)].TableDescriptor;
            var data = Column(td, "Data");

            data.Should().NotBeNull();
            data.DbType.Should().Be(DbType.String);
            data.Nullable.Should().BeFalse("Nullable defaults to false");
            data.Json.Should().NotBeNull();
            data.Json.ClrType.Should().Be(typeof(Profile));
            data.Json.Indexes.Should().BeEmpty();
            // the column name is derived from the property when Field is not set
            data.Name.Should().NotBeNullOrEmpty();
        }
    }
}
