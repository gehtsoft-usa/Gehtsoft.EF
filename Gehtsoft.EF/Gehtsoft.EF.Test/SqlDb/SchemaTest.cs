using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb
{
    public class SchemaTest : IClassFixture<SchemaTest.Fixture>
    {
        #region entities
        [Entity(Scope = "SchemaTest", Table = "st_entity")]
        public class Entity
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty(Size = 32, Sorted = true)]
            public string Name { get; set; }
        }

        [Entity(Scope = "SchemaTest", Table = "st_view", View = true, Metadata = typeof(ViewMetadata))]
        public class View
        {
            [EntityProperty(Field = "v_id")]
            public int ID { get; set; }

            [EntityProperty(Field = "v_name")]
            public string Name { get; set; }
        }

        public class ViewMetadata : IViewCreationMetadata
        {
            public SelectQueryBuilder GetSelectQuery(SqlDbConnection connection)
            {
                var td = AllEntities.Get<Entity>().TableDescriptor;
                var b = connection.GetSelectQueryBuilder(td);
                b.AddToResultset(td[0], "v_id");
                b.AddToResultset(td[1], "v_name");
                return b;
            }
        }
        #endregion

        #region fixture
        public class Fixture : SqlConnectionFixtureBase
        {
            public Fixture()
            {
            }

            protected override void ConfigureConnection(SqlDbConnection connection)
            {
                TearDownConnection(connection);
                using (var query = connection.GetQuery(connection.GetCreateTableBuilder(AllEntities.Get<Entity>().TableDescriptor)))
                    query.ExecuteNoData();

                using (var query = connection.GetQuery(
                    connection.GetCreateViewBuilder(AllEntities.Get<View>().TableDescriptor.Name,
                    AllEntities.Get<View>().TableDescriptor.Metadata.As<IViewCreationMetadata>().GetSelectQuery(connection))))
                    query.ExecuteNoData();

                base.ConfigureConnection(connection);
            }

            protected override void TearDownConnection(SqlDbConnection connection)
            {
                using (var query = connection.GetQuery(connection.GetDropViewBuilder(AllEntities.Get<View>().TableDescriptor.Name)))
                    query.ExecuteNoData();

                using (var query = connection.GetQuery(connection.GetDropTableBuilder(AllEntities.Get<Entity>().TableDescriptor)))
                    query.ExecuteNoData();

                base.TearDownConnection(connection);
            }
        }
        #endregion

        private readonly Fixture mFixture;

        public static IEnumerable<object[]> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);

        public SchemaTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Schema(string connectionName)
        {
            var entity = AllEntities.Get<Entity>().TableDescriptor;
            var view = AllEntities.Get<View>().TableDescriptor;

            var connection = mFixture.GetInstance(connectionName);
            var schema = connection.Schema();

            var e = Array.Find(schema, td => td.Name.Equals(entity.Name, StringComparison.OrdinalIgnoreCase));
            e.Should().NotBeNull();
            e.View.Should().BeFalse();
            e.Count.Should().Be(2);
            e[0].Name.Should().Be(entity[0].Name, StringComparison.OrdinalIgnoreCase);
            e[1].Name.Should().Be(entity[1].Name, StringComparison.OrdinalIgnoreCase);

            e = Array.Find(schema, td => td.Name.Equals(view.Name, StringComparison.OrdinalIgnoreCase));
            e.Should().NotBeNull();
            e.View.Should().BeTrue();
            e.Count.Should().Be(2);
            e[0].Name.Should().Be(view[0].Name, StringComparison.OrdinalIgnoreCase);
            e[1].Name.Should().Be(view[1].Name, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task SchemaAsync(string connectionName)
        {
            var entity = AllEntities.Get<Entity>().TableDescriptor;
            var view = AllEntities.Get<View>().TableDescriptor;

            var connection = mFixture.GetInstance(connectionName);
            var schema = await connection.SchemaAsync();

            var e = Array.Find(schema, td => td.Name.Equals(entity.Name, StringComparison.OrdinalIgnoreCase));
            e.Should().NotBeNull();
            e.View.Should().BeFalse();
            e.Count.Should().Be(2);
            e[0].Name.Should().Be(entity[0].Name, StringComparison.OrdinalIgnoreCase);
            e[1].Name.Should().Be(entity[1].Name, StringComparison.OrdinalIgnoreCase);

            e = Array.Find(schema, td => td.Name.Equals(view.Name, StringComparison.OrdinalIgnoreCase));
            e.Should().NotBeNull();
            e.View.Should().BeTrue();
            e.Count.Should().Be(2);
            e[0].Name.Should().Be(view[0].Name, StringComparison.OrdinalIgnoreCase);
            e[1].Name.Should().Be(view[1].Name, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "+sqlite")]
        public void SchemaArrayExtensions(string connectionName)
        {
            var entity = AllEntities.Get<Entity>().TableDescriptor;
            var view = AllEntities.Get<View>().TableDescriptor;

            var connection = mFixture.GetInstance(connectionName);
            var schema = connection.Schema();

            schema.Find(entity.Name)
                .Should().NotBeNull()
                         .And.Subject.As<TableDescriptor>().Name.Should().Be(entity.Name);

            schema.Contains(entity.Name).Should().BeTrue();
            schema.Find("nonexistent").Should().BeNull();
            schema.Contains("nonexistent").Should().BeFalse();

            schema.ContainsView(view.Name).Should().BeTrue();
            schema.ContainsView(entity.Name).Should().BeFalse();

            schema.Find(entity.Name, entity[0].Name)
                .Should().NotBeNull()
                .And.Subject.As<TableDescriptor.ColumnInfo>()
                    .Name.Should().Be(entity[0].Name);

            schema.Find(entity.Name, "nonexistent").Should().BeNull();
            schema.Find("nonexistent", "nonexistent").Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void CheckTable(string connectionName)
        {
            var entity = AllEntities.Get<Entity>().TableDescriptor;
            var connection = mFixture.GetInstance(connectionName);
            connection.DoesObjectExist(entity.Name, null, "table").Should().BeTrue();

            connection.DoesObjectExist("nonexistent", null, "table").Should().BeFalse();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task CheckTableAsync(string connectionName)
        {
            var entity = AllEntities.Get<Entity>().TableDescriptor;
            var connection = mFixture.GetInstance(connectionName);
            (await connection.DoesObjectExistAsync(entity.Name, null, "table")).Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void CheckView(string connectionName)
        {
            var view = AllEntities.Get<View>().TableDescriptor;
            var connection = mFixture.GetInstance(connectionName);
            connection.DoesObjectExist(view.Name, null, "view").Should().BeTrue();
            connection.DoesObjectExist("nonexistent", null, "view").Should().BeFalse();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void CheckColumn(string connectionName)
        {
            var entity = AllEntities.Get<Entity>().TableDescriptor;
            var view = AllEntities.Get<View>().TableDescriptor;

            var connection = mFixture.GetInstance(connectionName);
            connection.DoesObjectExist(entity.Name, entity[0].Name, "column").Should().BeTrue();
            connection.DoesObjectExist(entity.Name, entity[1].Name, "column").Should().BeTrue();

            connection.DoesObjectExist(entity.Name, "nonexistent", "column").Should().BeFalse();
            connection.DoesObjectExist("nonexistent", "nonexistent", "column").Should().BeFalse();

            connection.DoesObjectExist(view.Name, view[0].Name, "column").Should().BeTrue();
            connection.DoesObjectExist(view.Name, view[1].Name, "column").Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void CheckIndex(string connectionName)
        {
            var entity = AllEntities.Get<Entity>().TableDescriptor;
            var connection = mFixture.GetInstance(connectionName);
            connection.DoesObjectExist(entity.Name, entity[1].Name, "index").Should().BeTrue();

            connection.DoesObjectExist("nonexistent", entity[1].Name, "index").Should().BeFalse();
            connection.DoesObjectExist(entity.Name, "nonexistent", "index").Should().BeFalse();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void PreventInjections(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            ((Action)(() => connection.DoesObjectExist("'singlequote", null, "table"))).Should().Throw<ArgumentException>();
            ((Action)(() => connection.DoesObjectExist("\"doublequote", null, "table"))).Should().Throw<ArgumentException>();
            ((Action)(() => connection.DoesObjectExist("r", "'singlequote", "table"))).Should().Throw<ArgumentException>();
            ((Action)(() => connection.DoesObjectExist("r", "\"doublequote", "table"))).Should().Throw<ArgumentException>();

            ((Action)(() => connection.DoesObjectExistAsync("'singlequote", null, "table"))).Should().Throw<ArgumentException>();
            ((Action)(() => connection.DoesObjectExistAsync("\"doublequote", null, "table"))).Should().Throw<ArgumentException>();
            ((Action)(() => connection.DoesObjectExistAsync("r", "'singlequote", "table"))).Should().Throw<ArgumentException>();
            ((Action)(() => connection.DoesObjectExistAsync("r", "\"doublequote", "table"))).Should().Throw<ArgumentException>();
        }
    }
}

