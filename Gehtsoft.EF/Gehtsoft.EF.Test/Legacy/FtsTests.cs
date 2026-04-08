using System.Threading.Tasks;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.FTS;
using Gehtsoft.EF.Utils;
using Gehtsoft.Tools2.Extensions;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Legacy
{
    public class FtsTests : IClassFixture<FtsTests.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public FtsTests(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = "")
            => SqlConnectionSources.SqlConnectionNames(flags);

        [Fact]
        public void TestWordParser()
        {
            string[] words = StringExtensions.ParseToWords("");
            words.Length.Should().Be(0);
            words = StringExtensions.ParseToWords("", true);
            words.Length.Should().Be(0);
            words = StringExtensions.ParseToWords("- Hello, said человек в соломенной шляпе. I didn't wanted to say 'Good buy', I wanted to say 'I'm very busy'", false);
            words.Length.Should().Be(20);
            words.Should().Contain("Hello");
            words.Should().Contain("шляпе");
            words.Should().Contain("didn't");
            words.Should().Contain("I'm");
            words.Should().Contain("busy");

            words = StringExtensions.ParseToWords("- Hello, said человек в соломен% шляпе. I didn't wanted to say 'Good buy', I wanted to say 'I'm very busy'", true);
            words.Length.Should().Be(20);
            words.Should().Contain("Hello");
            words.Should().Contain("соломен%");
            words.Should().Contain("шляпе");
            words.Should().Contain("didn't");
            words.Should().Contain("I'm");
            words.Should().Contain("busy");
        }

        [Entity]
        public class FtsEntity
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty(Size = 128)]
            public string Text { get; set; }
        }

        private static bool Contains(string type, string id, IEntityAccessor<FtsObjectEntity> coll)
        {
            if (coll == null)
                return false;
            if (coll.Count == 0)
                return false;
            foreach (FtsObjectEntity obj in coll)
            {
                if (obj.ObjectID == id && obj.ObjectType == type)
                    return true;
            }
            return false;
        }

        private static bool Contains(string word, IEntityAccessor<FtsWordEntity> coll)
        {
            word = word.ToUpper();
            if (coll == null)
                return false;
            if (coll.Count == 0)
                return false;
            foreach (FtsWordEntity obj in coll)
            {
                if (obj.Word == word)
                    return true;
            }
            return false;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-oracle")]
        public void FtsSync(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            connection.FtsDropTables();
            connection.DoesFtsTableExist().Should().BeFalse();
            connection.FtsCreateTables();
            connection.DoesFtsTableExist().Should().BeTrue();

            connection.FtsSetObjectText("type1", "object1", "moody moon loves foxy fox knox");
            connection.FtsSetObjectText("type1", "object2", "why foxes don't like carrots and likes pepper?");
            connection.FtsSetObjectText("type2", "object1", "петя ел перец и пряники");
            connection.FtsSetObjectText("type2", "object2", "хорошо в краю родном пахнет хлебом и... перец растет хорошо");
            connection.FtsSetObjectText("type2", "object3", "it's nice in home country, bread smells good and pepper grows well");

            FtsObjectEntityCollection coll;
            FtsWordEntityCollection words;

            coll = connection.FtsGetObjects("knox", false);
            coll.Count.Should().Be(1);

            coll = connection.FtsGetObjects("equinox", true);
            coll.Count.Should().Be(0);

            connection.FtsSetObjectText("type1", "object1", "why moody moon loves foxy fox and don't like dogs?");

            coll = connection.FtsGetObjects("knox", false);
            coll.Count.Should().Be(0);

            words = connection.FtsGetWords("knox", 0, 0);
            words.Should().NotBeNull();
            words.Count.Should().Be(1);

            connection.FtsCleanupWords();
            words = connection.FtsGetWords("knox", 0, 0);
            words.Should().NotBeNull();
            words.Count.Should().Be(0);

            coll = connection.FtsGetObjects("перец пряники", false);
            coll.Count.Should().Be(2);
            Contains("type2", "object1", coll).Should().BeTrue();
            Contains("type2", "object2", coll).Should().BeTrue();

            connection.FtsCountObjects("перец пряники", false).Should().Be(2);

            coll = connection.FtsGetObjects("петя", false, null, 10, 0);
            coll.Count.Should().Be(1);

            coll = connection.FtsGetObjects("петя", false, new string[] { "type2", "type1" }, 10, 0);
            coll.Count.Should().Be(1);

            coll = connection.FtsGetObjects("перец пряники", true);
            coll.Count.Should().Be(1);
            Contains("type2", "object1", coll).Should().BeTrue();

            coll = connection.FtsGetObjects("п% х%", true);
            coll.Count.Should().Be(1);
            Contains("type2", "object2", coll).Should().BeTrue();

            coll = connection.FtsGetObjects("п% d%", true);
            coll.Count.Should().Be(0);

            coll = connection.FtsGetObjects("п% d%", false);
            coll.Count.Should().Be(4);
            Contains("type1", "object1", coll).Should().BeTrue();
            Contains("type1", "object2", coll).Should().BeTrue();
            Contains("type2", "object1", coll).Should().BeTrue();
            Contains("type2", "object2", coll).Should().BeTrue();

            coll = connection.FtsGetObjects("п% d%", false, new string[] { "type1", "type2" });
            coll.Count.Should().Be(4);
            Contains("type1", "object1", coll).Should().BeTrue();
            Contains("type1", "object2", coll).Should().BeTrue();
            Contains("type2", "object1", coll).Should().BeTrue();
            Contains("type2", "object2", coll).Should().BeTrue();

            coll = connection.FtsGetObjects("п% d%", false, new string[] { "type2" });
            coll.Count.Should().Be(2);
            Contains("type2", "object1", coll).Should().BeTrue();
            Contains("type2", "object2", coll).Should().BeTrue();

            connection.FtsDeleteObject("type1", "object1");

            coll = connection.FtsGetObjects("п% d%", false);
            Contains("type1", "object1", coll).Should().BeFalse();

            words = connection.FtsGetWords("f%", 0, 0);
            words.Should().NotBeNull();
            words.Count.Should().Be(3);
            Contains("fox", words).Should().BeTrue();
            Contains("foxy", words).Should().BeTrue();
            Contains("foxes", words).Should().BeTrue();

            words = connection.FtsGetWords("f%", 1, 0);
            words.Should().NotBeNull();
            words.Count.Should().Be(1);
            Contains("fox", words).Should().BeTrue();

            words = connection.FtsGetWords("moody", 0, 0);
            words.Should().NotBeNull();
            words.Count.Should().Be(1);

            connection.FtsCleanupWords();

            words = connection.FtsGetWords("moody", 0, 0);
            words.Should().NotBeNull();
            words.Count.Should().Be(0);

            using (var query = connection.GetDropEntityQuery<FtsEntity>())
                query.Execute();

            using (var query = connection.GetCreateEntityQuery<FtsEntity>())
                query.Execute();

            FtsEntity entity = new FtsEntity() { Text = "The text number one" };
            using (var query = connection.GetInsertEntityQuery<FtsEntity>())
                query.Execute(entity);
            connection.FtsSetObjectText("t1", entity.ID.ToString(), entity.Text);

            entity = new FtsEntity() { Text = "The string number two" };
            using (var query = connection.GetInsertEntityQuery<FtsEntity>())
                query.Execute(entity);
            connection.FtsSetObjectText("t1", entity.ID.ToString(), entity.Text);

            entity = new FtsEntity() { Text = "The poem number three" };
            using (var query = connection.GetInsertEntityQuery<FtsEntity>())
                query.Execute(entity);
            connection.FtsSetObjectText("t1", entity.ID.ToString(), entity.Text);

            entity = new FtsEntity() { Text = "The story number four" };
            using (var query = connection.GetInsertEntityQuery<FtsEntity>())
                query.Execute(entity);
            connection.FtsSetObjectText("t1", entity.ID.ToString(), entity.Text);

            entity = new FtsEntity() { Text = "The text number five" };
            using (var query = connection.GetInsertEntityQuery<FtsEntity>())
                query.Execute(entity);
            connection.FtsSetObjectText("t1", entity.ID.ToString(), entity.Text);

            using (var query = connection.GetSelectEntitiesQuery<FtsEntity>())
            {
                query.Where.AddFtsSearch("text five", FtsQueryExtension.QueryType.AnyWordInclude, "t1");
                query.AddOrderBy(nameof(FtsEntity.ID));
                EntityCollection<FtsEntity> rc = query.ReadAll<FtsEntity>();

                rc.Count.Should().Be(2);
                rc[0].ID.Should().Be(1);
                rc[1].ID.Should().Be(5);
            }

            using (var query = connection.GetSelectEntitiesQuery<FtsEntity>())
            {
                query.Where.AddFtsSearch("text five", FtsQueryExtension.QueryType.AllWordsInclude, "t1");
                query.AddOrderBy(nameof(FtsEntity.ID));
                EntityCollection<FtsEntity> rc = query.ReadAll<FtsEntity>();

                rc.Count.Should().Be(1);
                rc[0].ID.Should().Be(5);
            }

            using (var query = connection.GetSelectEntitiesQuery<FtsEntity>())
            {
                query.Where.AddFtsSearch("number", FtsQueryExtension.QueryType.AllWordsInclude, "t1");
                query.AddOrderBy(nameof(FtsEntity.ID));
                EntityCollection<FtsEntity> rc = query.ReadAll<FtsEntity>();

                rc.Count.Should().Be(5);
            }

            using (var query = connection.GetSelectEntitiesQuery<FtsEntity>())
            {
                query.Where.AddFtsSearch("text", FtsQueryExtension.QueryType.AnyWordExclude, "t1");
                query.AddOrderBy(nameof(FtsEntity.ID));
                EntityCollection<FtsEntity> rc = query.ReadAll<FtsEntity>();

                rc.Count.Should().Be(3);
                foreach (var v in rc)
                    v.Text.Contains("text").Should().BeFalse();
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-oracle")]
        public async Task FtsAsync(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            await connection.FtsDropTablesAsync();
            (await connection.DoesFtsTableExistAsync()).Should().BeFalse();
            await connection.FtsCreateTablesAsync();
            (await connection.DoesFtsTableExistAsync()).Should().BeTrue();

            await connection.FtsSetObjectTextAsync("type1", "object1", "moody moon loves foxy fox knox");
            await connection.FtsSetObjectTextAsync("type1", "object2", "why foxes don't like carrots and likes pepper?");
            await connection.FtsSetObjectTextAsync("type2", "object1", "петя ел перец и пряники");
            await connection.FtsSetObjectTextAsync("type2", "object2", "хорошо в краю родном пахнет хлебом и... перец растет хорошо");
            await connection.FtsSetObjectTextAsync("type2", "object3", "it's nice in home country, bread smells good and pepper grows well");

            FtsObjectEntityCollection coll;
            FtsWordEntityCollection words;

            coll = await connection.FtsGetObjectsAsync("knox", false);
            coll.Count.Should().Be(1);

            coll = await connection.FtsGetObjectsAsync("equinox", true);
            coll.Count.Should().Be(0);

            await connection.FtsSetObjectTextAsync("type1", "object1", "why moody moon loves foxy fox and don't like dogs?");

            coll = await connection.FtsGetObjectsAsync("knox", false);
            coll.Count.Should().Be(0);

            words = await connection.FtsGetWordsAsync("knox", 0, 0);
            words.Should().NotBeNull();
            words.Count.Should().Be(1);

            await connection.FtsCleanupWordsAsync();
            words = await connection.FtsGetWordsAsync("knox", 0, 0);
            words.Should().NotBeNull();
            words.Count.Should().Be(0);

            coll = await connection.FtsGetObjectsAsync("перец пряники", false);
            coll.Count.Should().Be(2);
            Contains("type2", "object1", coll).Should().BeTrue();
            Contains("type2", "object2", coll).Should().BeTrue();

            (await connection.FtsCountObjectsAsync("перец пряники", false)).Should().Be(2);

            coll = await connection.FtsGetObjectsAsync("петя", false, null, 10, 0);
            coll.Count.Should().Be(1);

            coll = await connection.FtsGetObjectsAsync("петя", false, new string[] { "type2", "type1" }, 10, 0);
            coll.Count.Should().Be(1);

            coll = await connection.FtsGetObjectsAsync("перец пряники", true);
            coll.Count.Should().Be(1);
            Contains("type2", "object1", coll).Should().BeTrue();

            coll = await connection.FtsGetObjectsAsync("п% х%", true);
            coll.Count.Should().Be(1);
            Contains("type2", "object2", coll).Should().BeTrue();

            coll = await connection.FtsGetObjectsAsync("п% d%", true);
            coll.Count.Should().Be(0);

            coll = connection.FtsGetObjects("п% d%", false);
            coll.Count.Should().Be(4);
            Contains("type1", "object1", coll).Should().BeTrue();
            Contains("type1", "object2", coll).Should().BeTrue();
            Contains("type2", "object1", coll).Should().BeTrue();
            Contains("type2", "object2", coll).Should().BeTrue();

            coll = await connection.FtsGetObjectsAsync("п% d%", false, new string[] { "type1", "type2" });
            coll.Count.Should().Be(4);
            Contains("type1", "object1", coll).Should().BeTrue();
            Contains("type1", "object2", coll).Should().BeTrue();
            Contains("type2", "object1", coll).Should().BeTrue();
            Contains("type2", "object2", coll).Should().BeTrue();

            coll = await connection.FtsGetObjectsAsync("п% d%", false, new string[] { "type2" });
            coll.Count.Should().Be(2);
            Contains("type2", "object1", coll).Should().BeTrue();
            Contains("type2", "object2", coll).Should().BeTrue();

            await connection.FtsDeleteObjectAsync("type1", "object1");

            coll = await connection.FtsGetObjectsAsync("п% d%", false);
            Contains("type1", "object1", coll).Should().BeFalse();

            words = await connection.FtsGetWordsAsync("f%", 0, 0);
            words.Should().NotBeNull();
            words.Count.Should().Be(3);
            Contains("fox", words).Should().BeTrue();
            Contains("foxy", words).Should().BeTrue();
            Contains("foxes", words).Should().BeTrue();

            words = await connection.FtsGetWordsAsync("f%", 1, 0);
            words.Should().NotBeNull();
            words.Count.Should().Be(1);
            Contains("fox", words).Should().BeTrue();

            words = await connection.FtsGetWordsAsync("moody", 0, 0);
            words.Should().NotBeNull();
            words.Count.Should().Be(1);

            await connection.FtsCleanupWordsAsync();

            words = await connection.FtsGetWordsAsync("moody", 0, 0);
            words.Should().NotBeNull();
            words.Count.Should().Be(0);

            using (var query = connection.GetDropEntityQuery<FtsEntity>())
                await query.ExecuteAsync();

            using (var query = connection.GetCreateEntityQuery<FtsEntity>())
                await query.ExecuteAsync();

            FtsEntity entity = new FtsEntity() { Text = "The text number one" };
            using (var query = connection.GetInsertEntityQuery<FtsEntity>())
                await query.ExecuteAsync(entity);

            await connection.FtsSetObjectTextAsync("t1", entity.ID.ToString(), entity.Text);

            entity = new FtsEntity() { Text = "The string number two" };
            using (var query = connection.GetInsertEntityQuery<FtsEntity>())
                await query.ExecuteAsync(entity);
            await connection.FtsSetObjectTextAsync("t1", entity.ID.ToString(), entity.Text);

            entity = new FtsEntity() { Text = "The poem number three" };
            using (var query = connection.GetInsertEntityQuery<FtsEntity>())
                await query.ExecuteAsync(entity);
            await connection.FtsSetObjectTextAsync("t1", entity.ID.ToString(), entity.Text);

            entity = new FtsEntity() { Text = "The story number four" };
            using (var query = connection.GetInsertEntityQuery<FtsEntity>())
                await query.ExecuteAsync(entity);
            await connection.FtsSetObjectTextAsync("t1", entity.ID.ToString(), entity.Text);

            entity = new FtsEntity() { Text = "The text number five" };
            using (var query = connection.GetInsertEntityQuery<FtsEntity>())
                await query.ExecuteAsync(entity);
            await connection.FtsSetObjectTextAsync("t1", entity.ID.ToString(), entity.Text);

            using (var query = connection.GetSelectEntitiesQuery<FtsEntity>())
            {
                query.Where.AddFtsSearch("text five", FtsQueryExtension.QueryType.AnyWordInclude, "t1");
                query.AddOrderBy(nameof(FtsEntity.ID));
                EntityCollection<FtsEntity> rc = await query.ReadAllAsync<FtsEntity>();

                rc.Count.Should().Be(2);
                rc[0].ID.Should().Be(1);
                rc[1].ID.Should().Be(5);
            }

            using (var query = connection.GetSelectEntitiesQuery<FtsEntity>())
            {
                query.Where.AddFtsSearch("text five", FtsQueryExtension.QueryType.AllWordsInclude, "t1");
                query.AddOrderBy(nameof(FtsEntity.ID));
                EntityCollection<FtsEntity> rc = await query.ReadAllAsync<FtsEntity>();

                rc.Count.Should().Be(1);
                rc[0].ID.Should().Be(5);
            }

            using (var query = connection.GetSelectEntitiesQuery<FtsEntity>())
            {
                query.Where.AddFtsSearch("number", FtsQueryExtension.QueryType.AllWordsInclude, "t1");
                query.AddOrderBy(nameof(FtsEntity.ID));
                EntityCollection<FtsEntity> rc = await query.ReadAllAsync<FtsEntity>();

                rc.Count.Should().Be(5);
            }

            using (var query = connection.GetSelectEntitiesQuery<FtsEntity>())
            {
                query.Where.AddFtsSearch("text", FtsQueryExtension.QueryType.AnyWordExclude, "t1");
                query.AddOrderBy(nameof(FtsEntity.ID));
                EntityCollection<FtsEntity> rc = await query.ReadAllAsync<FtsEntity>();

                rc.Count.Should().Be(3);
                foreach (var v in rc)
                    v.Text.Contains("text").Should().BeFalse();
            }
        }
    }
}
