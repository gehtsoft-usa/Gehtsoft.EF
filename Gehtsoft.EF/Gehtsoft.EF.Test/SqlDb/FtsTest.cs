using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.FTS;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb
{
    [TestCaseOrderer(TestOrderAttributeOrderer.CLASS, TestOrderAttributeOrderer.ASSEMBLY)]
    public class FtsTest : IClassFixture<FtsTest.Fixture>
    {
        private readonly Fixture mFixture;

        [Entity(Scope = "fts_test", Table = "fts_test")]
        public class Entity
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty(Size = 16)]
            public string Name { get; set; }
        }

        public class Fixture : ConnectionFixtureBase
        {
            public Fixture() : base()
            {
            }

            protected override void ConfigureConnection(SqlDbConnection connection)
            {
                connection.FtsDropTables();
                connection.FtsCreateTables();

                var td = AllEntities.Get<Entity>().TableDescriptor;
                
                using (var query = connection.GetQuery(connection.GetDropTableBuilder(td)))
                    query.ExecuteNoData();

                using (var query = connection.GetQuery(connection.GetCreateTableBuilder(td)))
                    query.ExecuteNoData();
            }
        }

        public static IEnumerable<object[]> ConnectionNames(string flags = null) => SqlConnectionSources.ConnectionNames(flags);

        public FtsTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        [TestOrder(1)]
        public async Task CheckTablesExist(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            connection.DoesFtsTableExist().Should().BeTrue();
            (await connection.DoesFtsTableExistAsync()).Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        [TestOrder(1)]
        public async Task AddWords(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            Exception x = null;
            try
            {
                connection.FtsSetObjectText("type1", "object1", "The quick brown fox jumps over the lazy dog");
                connection.FtsSetObjectText("type1", "object2", "Jackdaws love my big sphinx of quartz");
                await connection.FtsSetObjectTextAsync("type2", "object1", "Waltz, bad nymph, for quick jigs vex");
                await connection.FtsSetObjectTextAsync("type2", "object2", "Fox, don't pack my box with five dozen liquor jugs");
            }
            catch (Exception e)
            {
                x = e;
            }
            x.Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        [TestOrder(2)]
        public void GetAllWords(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                AddWords(connectionName).Wait();

            var connection = mFixture.GetInstance(connectionName);

            var words = connection.FtsGetWords("%", 0, 0);

            words.Select(w => w.Word)
                .Should().HaveCount(29)
                .And.Contain("THE")
                .And.Contain("QUICK")
                .And.Contain("BROWN")
                .And.Contain("FOX")
                .And.Contain("JUMPS")
                .And.Contain("OVER")
                .And.Contain("LAZY")
                .And.Contain("DOG")
                .And.Contain("JACKDAWS")
                .And.Contain("LOVE")
                .And.Contain("MY")
                .And.Contain("QUARTZ")
                .And.Contain("BIG")
                .And.Contain("SPHINX")
                .And.Contain("OF")
                .And.Contain("WALTZ")
                .And.Contain("BAD")
                .And.Contain("NYMPH")
                .And.Contain("FOR")
                .And.Contain("JIGS")
                .And.Contain("VEX")
                .And.Contain("DON'T")
                .And.Contain("PACK")
                .And.Contain("JUGS")
                .And.Contain("LIQUOR")
                .And.Contain("DOZEN")
                .And.Contain("BOX")
                .And.Contain("WITH")
                .And.Contain("FIVE")
                .And.BeInAscendingOrder();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        [TestOrder(2)]
        public async Task GetAllWords_SkipLimit(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                await AddWords(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var words = await connection.FtsGetWordsAsync("%", 3, 1);

            words.Select(w => w.Word)
                .Should().HaveCount(3)
                .And.Contain("BIG")
                .And.Contain("BOX")
                .And.Contain("BROWN")
                .And.BeInAscendingOrder();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        [TestOrder(2)]
        public async Task GetAllWords_Mask(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                await AddWords(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var words = await connection.FtsGetWordsAsync("f%", 0, 0);

            words.Select(w => w.Word)
                .Should().HaveCount(3)
                .And.Contain("FOX")
                .And.Contain("FOR")
                .And.Contain("FIVE")
                .And.BeInAscendingOrder();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        [TestOrder(2)]
        public async Task FindObject_ByOneWord_Only_Success(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                await AddWords(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var obj = connection.FtsGetObjects("jumps");

            obj.Should().HaveCount(1)
                .And.HaveElementMatching(o => o.ObjectType == "type1" && o.ObjectID == "object1");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        [TestOrder(2)]
        public async Task FindObject_ByOneWord_Only_Fails(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                await AddWords(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var obj = await connection.FtsGetObjectsAsync("fax");

            obj.Should().HaveCount(0);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        [TestOrder(2)]
        public async Task FindObject_ByOneWord_Only_Many(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                await AddWords(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var obj = connection.FtsGetObjects("quick fox");

            obj.Should().HaveCount(3)
                .And.HaveElementMatching(o => o.ObjectType == "type1" && o.ObjectID == "object1")
                .And.HaveElementMatching(o => o.ObjectType == "type2" && o.ObjectID == "object1")
                .And.HaveElementMatching(o => o.ObjectType == "type2" && o.ObjectID == "object2");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        [TestOrder(2)]
        public async Task FindObject_ByOneWord_Only_LimitByType(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                await AddWords(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var obj = connection.FtsGetObjects("quick fox", types: new[] { "type1" });

            obj.Should().HaveCount(1)
                .And.HaveElementMatching(o => o.ObjectType == "type1" && o.ObjectID == "object1");

            obj = connection.FtsGetObjects("quick fox", types: new[] { "type2" });

            obj.Should().HaveCount(2)
                .And.HaveElementMatching(o => o.ObjectType == "type2" && o.ObjectID == "object1")
                .And.HaveElementMatching(o => o.ObjectType == "type2" && o.ObjectID == "object2");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        [TestOrder(2)]
        public async Task FindObject_ByAllWords(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                await AddWords(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var obj = await connection.FtsGetObjectsAsync("quick fox", allWords: true);
            (await connection.FtsCountObjectsAsync("quick fox", allWords: true)).Should().Be(1);

            obj.Should().HaveCount(1)
                .And.HaveElementMatching(o => o.ObjectType == "type1" && o.ObjectID == "object1");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        [TestOrder(2)]
        public async Task FindObject_ByMask(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                await AddWords(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var obj = connection.FtsGetObjects("fo%");

            connection.FtsCountObjects("fo%", allWords: true).Should().Be(3);

            obj.Should().HaveCount(3)
                .And.HaveElementMatching(o => o.ObjectType == "type1" && o.ObjectID == "object1")
                .And.HaveElementMatching(o => o.ObjectType == "type2" && o.ObjectID == "object1")
                .And.HaveElementMatching(o => o.ObjectType == "type2" && o.ObjectID == "object2");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        [TestOrder(3)]
        public async Task AddCyrillic(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                await AddWords(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            ((Action)(() => connection.FtsSetObjectText("rus", "text1", "это русский текст"))).Should().NotThrow();
            ((Action)(() => connection.FtsSetObjectText("rus", "text2", "это сильно не русский текст"))).Should().NotThrow();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        [TestOrder(4)]
        public async Task CheckCyrillicWords(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                await AddCyrillic(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var words = connection.FtsGetWords("%", 0, 0);
            words.Select(w => w.Word)
                .Should().HaveCount(29 + 5)
                .And.Contain("ЭТО")
                .And.Contain("НЕ")
                .And.Contain("СИЛЬНО")
                .And.Contain("РУССКИЙ")
                .And.Contain("ТЕКСТ");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        [TestOrder(4)]
        public async Task SearchCyrillicWords(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                await AddCyrillic(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var objs = connection.FtsGetObjects("сильно");
            objs.Should().HaveCount(1)
                .And.HaveElementMatching(e => e.ObjectType == "rus" && e.ObjectID == "text2");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        [TestOrder(5)]
        public async Task Delete(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                await AddCyrillic(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            connection.FtsDeleteObject("rus", "text2");

            var objs = connection.FtsGetObjects("сильно");
            objs.Should().HaveCount(0);

            connection.FtsCleanupWords();

            var words = connection.FtsGetWords("%", 0, 0);
            words.Select(w => w.Word)
                .Should().HaveCount(29 + 3)
                .And.Contain("ЭТО")
                .And.Contain("РУССКИЙ")
                .And.Contain("ТЕКСТ");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        [TestOrder(6)]
        public async Task DeleteAsync(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                await Delete(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            await connection.FtsDeleteObjectAsync("rus", "text1");

            var objs = connection.FtsGetObjects("текст");
            objs.Should().HaveCount(0);

            await connection.FtsCleanupWordsAsync();

            var words = connection.FtsGetWords("%", 0, 0);
            words.Select(w => w.Word)
                .Should().HaveCount(29);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        [TestOrder(10)]
        public async Task ConnectToEntities(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                await AddWords(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            Entity e1, e2;
           
            using (var query = connection.GetInsertEntityQuery<Entity>())
            {
                e1 = new Entity() { Name = "entity1" };
                query.Execute(e1);
                e2 = new Entity() { Name = "entity2" };
                query.Execute(e2);
            }

            connection.FtsSetObjectText("connection", e1.ID.ToString(), "the text for the first entity");
            connection.FtsSetObjectText("connection", e2.ID.ToString(), "the text for the second entity");

            using (var query = connection.GetSelectEntitiesQuery<Entity>())
            {
                query.Where.AddFtsSearch("second", FtsQueryExtension.QueryType.AllWordsInclude, "connection");
                query.Execute();
                var entities = query.ReadAll<Entity>();
                entities.Should()
                    .HaveCount(1)
                    .And.HaveElementMatching(e => e.ID == e2.ID);
            }

            using (var query = connection.GetSelectEntitiesQuery<Entity>())
            {
                query.Where.AddFtsSearch("second", FtsQueryExtension.QueryType.AllWordsExclude, "connection");
                query.Execute();
                var entities = query.ReadAll<Entity>();
                entities.Should()
                    .HaveCount(1)
                    .And.HaveElementMatching(e => e.ID == e1.ID);
            }
        }
    }
}

