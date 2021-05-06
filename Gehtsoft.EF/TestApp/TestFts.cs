using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.FTS;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;
using Gehtsoft.Tools.TypeUtils;
using NUnit.Framework;
using System.Threading.Tasks;

namespace TestApp
{
    [TestFixture]
    public class TestFts
    {
        [Test]
        public void TestWordParser()
        {
            string[] words = StringUtils.ParseToWords("");
            Assert.AreEqual(0, words.Length);
            words = StringUtils.ParseToWords("", true);
            Assert.AreEqual(0, words.Length);
            words = StringUtils.ParseToWords("- Hello, said человек в соломенной шляпе. I didn't wanted to say 'Good buy', I wanted to say ’I’m very busy’", false);
            Assert.AreEqual(20, words.Length);
            Assert.Contains("Hello", words);
            Assert.Contains("шляпе", words);
            Assert.Contains("didn't", words);
            Assert.Contains("I’m", words);
            Assert.Contains("busy", words);

            words = StringUtils.ParseToWords("- Hello, said человек в соломен% шляпе. I didn't wanted to say 'Good buy', I wanted to say ’I’m very busy’", true);
            Assert.AreEqual(20, words.Length);
            Assert.Contains("Hello", words);
            Assert.Contains("соломен%", words);
            Assert.Contains("шляпе", words);
            Assert.Contains("didn't", words);
            Assert.Contains("I’m", words);
            Assert.Contains("busy", words);
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

        [Test]
        public void TestLevenstein()
        {
            Assert.AreEqual(0, "dow".DistanceTo("dow"));
            Assert.AreEqual(1, "dow".DistanceTo("doe"));
            Assert.AreEqual(2, "dow".DistanceTo("deo"));
            Assert.AreEqual(8, "catalupe".DistanceTo("oregon"));
        }

        [Explicit]
        [Test]
        public void TestLevensteinPerformance()
        {
            const int nsamples = 120000;
            const int nwords = 100;
            const int minlength = 2;
            const int maxlength = 10;

            Random r = new Random((int)DateTime.Now.Ticks & 0xffff);

            string[] samples = new string[nsamples];
            for (int i = 0; i < nsamples; i++)
                samples[i] = RandomWord(r, minlength, maxlength);
            string[] words = new string[nwords];
            for (int i = 0; i < nwords; i++)
                words[i] = RandomWord(r, minlength, maxlength);

            Stopwatch sw = new Stopwatch();
            sw.Start();

            int distance = 0;
            for (int i = 0; i < nwords; i++)
            {
                for (int j = 0; j < nsamples; j++)
                {
                    distance += words[i].DistanceTo(samples[j]);
                }
            }
            sw.Stop();
            Assert.AreNotEqual(0, distance);

            Console.WriteLine($"Test size {nsamples} samples, {nwords} words");
            Console.WriteLine($"Total test {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"One word per {sw.ElapsedMilliseconds * 1.0 / nwords} ms");
        }

        private static string RandomWord(Random r, int minlength, int maxlength)
        {
            int length = minlength + r.Next(maxlength - minlength + 1);
            char[] word = new char[length];
            for (int i = 0; i < word.Length; i++)
                word[i] = (char)r.Next('A', 'Z' + 1);
            return new string(word);
        }

        [Entity]
        public class FtsEntity
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty(Size = 128)]
            public string Text { get; set; }
        }

        public static void DoTestFts(SqlDbConnection connection)
        {
            connection.FtsDropTables();
            Assert.IsFalse(connection.DoesFtsTableExist());
            connection.FtsCreateTables();
            Assert.IsTrue(connection.DoesFtsTableExist());

            connection.FtsSetObjectText("type1", "object1", "moody moon loves foxy fox knox");
            connection.FtsSetObjectText("type1", "object2", "why foxes don't like carrots and likes pepper?");
            connection.FtsSetObjectText("type2", "object1", "петя ел перец и пряники");
            connection.FtsSetObjectText("type2", "object2", "хорошо в краю родном пахнет хлебом и... перец растет хорошо");
            connection.FtsSetObjectText("type2", "object3", "it's nice in home country, bread smells good and pepper grows well");

            FtsObjectEntityCollection coll;
            FtsWordEntityCollection words;

            coll = connection.FtsGetObjects("knox", false);
            Assert.AreEqual(1, coll.Count);

            coll = connection.FtsGetObjects("equinox", true);
            Assert.AreEqual(0, coll.Count);

            connection.FtsSetObjectText("type1", "object1", "why moody moon loves foxy fox and don't like dogs?");

            coll = connection.FtsGetObjects("knox", false);
            Assert.AreEqual(0, coll.Count);

            words = connection.FtsGetWords("knox", 0, 0);
            Assert.IsNotNull(words);
            Assert.AreEqual(1, words.Count);

            connection.FtsCleanupWords();
            words = connection.FtsGetWords("knox", 0, 0);
            Assert.IsNotNull(words);
            Assert.AreEqual(0, words.Count);

            coll = connection.FtsGetObjects("перец пряники", false);
            Assert.AreEqual(2, coll.Count);
            Assert.IsTrue(Contains("type2", "object1", coll));
            Assert.IsTrue(Contains("type2", "object2", coll));

            Assert.AreEqual(2, connection.FtsCountObjects("перец пряники", false));

            coll = connection.FtsGetObjects("петя", false, null, 10, 0);
            Assert.AreEqual(1, coll.Count);

            coll = connection.FtsGetObjects("петя", false, new string[] { "type2", "type1" }, 10, 0);
            Assert.AreEqual(1, coll.Count);

            coll = connection.FtsGetObjects("перец пряники", true);
            Assert.AreEqual(1, coll.Count);
            Assert.IsTrue(Contains("type2", "object1", coll));

            coll = connection.FtsGetObjects("п% х%", true);
            Assert.AreEqual(1, coll.Count);
            Assert.IsTrue(Contains("type2", "object2", coll));

            coll = connection.FtsGetObjects("п% d%", true);
            Assert.AreEqual(0, coll.Count);

            coll = connection.FtsGetObjects("п% d%", false);
            Assert.AreEqual(4, coll.Count);
            Assert.IsTrue(Contains("type1", "object1", coll));
            Assert.IsTrue(Contains("type1", "object2", coll));
            Assert.IsTrue(Contains("type2", "object1", coll));
            Assert.IsTrue(Contains("type2", "object2", coll));

            coll = connection.FtsGetObjects("п% d%", false, new string[] { "type1", "type2" });
            Assert.AreEqual(4, coll.Count);
            Assert.IsTrue(Contains("type1", "object1", coll));
            Assert.IsTrue(Contains("type1", "object2", coll));
            Assert.IsTrue(Contains("type2", "object1", coll));
            Assert.IsTrue(Contains("type2", "object2", coll));

            coll = connection.FtsGetObjects("п% d%", false, new string[] { "type2" });
            Assert.AreEqual(2, coll.Count);
            Assert.IsTrue(Contains("type2", "object1", coll));
            Assert.IsTrue(Contains("type2", "object2", coll));

            connection.FtsDeleteObject("type1", "object1");

            coll = connection.FtsGetObjects("п% d%", false);
            Assert.IsFalse(Contains("type1", "object1", coll));

            words = connection.FtsGetWords("f%", 0, 0);
            Assert.IsNotNull(words);
            Assert.AreEqual(3, words.Count);
            Assert.IsTrue(Contains("fox", words));
            Assert.IsTrue(Contains("foxy", words));
            Assert.IsTrue(Contains("foxes", words));

            words = connection.FtsGetWords("f%", 1, 0);
            Assert.IsNotNull(words);
            Assert.AreEqual(1, words.Count);
            Assert.IsTrue(Contains("fox", words));

            words = connection.FtsGetWords("moody", 0, 0);
            Assert.IsNotNull(words);
            Assert.AreEqual(1, words.Count);

            connection.FtsCleanupWords();

            words = connection.FtsGetWords("moody", 0, 0);
            Assert.IsNotNull(words);
            Assert.AreEqual(0, words.Count);

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

                Assert.AreEqual(2, rc.Count);
                Assert.AreEqual(1, rc[0].ID);
                Assert.AreEqual(5, rc[1].ID);
            }

            using (var query = connection.GetSelectEntitiesQuery<FtsEntity>())
            {
                query.Where.AddFtsSearch("text five", FtsQueryExtension.QueryType.AllWordsInclude, "t1");
                query.AddOrderBy(nameof(FtsEntity.ID));
                EntityCollection<FtsEntity> rc = query.ReadAll<FtsEntity>();

                Assert.AreEqual(1, rc.Count);
                Assert.AreEqual(5, rc[0].ID);
            }

            using (var query = connection.GetSelectEntitiesQuery<FtsEntity>())
            {
                query.Where.AddFtsSearch("number", FtsQueryExtension.QueryType.AllWordsInclude, "t1");
                query.AddOrderBy(nameof(FtsEntity.ID));
                EntityCollection<FtsEntity> rc = query.ReadAll<FtsEntity>();

                Assert.AreEqual(5, rc.Count);
            }

            using (var query = connection.GetSelectEntitiesQuery<FtsEntity>())
            {
                query.Where.AddFtsSearch("text", FtsQueryExtension.QueryType.AnyWordExclude, "t1");
                query.AddOrderBy(nameof(FtsEntity.ID));
                EntityCollection<FtsEntity> rc = query.ReadAll<FtsEntity>();

                Assert.AreEqual(3, rc.Count);
                foreach (var v in rc)
                    Assert.IsFalse(v.Text.Contains("text"));
            }
        }

        public static async Task DoTestFtsAsync(SqlDbConnection connection)
        {
            await connection.FtsDropTablesAsync();
            Assert.IsFalse(await connection.DoesFtsTableExistAsync());
            await connection.FtsCreateTablesAsync();
            Assert.IsTrue(await connection.DoesFtsTableExistAsync());

            await connection.FtsSetObjectTextAsync("type1", "object1", "moody moon loves foxy fox knox");
            await connection.FtsSetObjectTextAsync("type1", "object2", "why foxes don't like carrots and likes pepper?");
            await connection.FtsSetObjectTextAsync("type2", "object1", "петя ел перец и пряники");
            await connection.FtsSetObjectTextAsync("type2", "object2", "хорошо в краю родном пахнет хлебом и... перец растет хорошо");
            await connection.FtsSetObjectTextAsync("type2", "object3", "it's nice in home country, bread smells good and pepper grows well");

            FtsObjectEntityCollection coll;
            FtsWordEntityCollection words;

            coll = await connection.FtsGetObjectsAsync("knox", false);
            Assert.AreEqual(1, coll.Count);

            coll = await connection.FtsGetObjectsAsync("equinox", true);
            Assert.AreEqual(0, coll.Count);

            await connection.FtsSetObjectTextAsync("type1", "object1", "why moody moon loves foxy fox and don't like dogs?");

            coll = await connection.FtsGetObjectsAsync("knox", false);
            Assert.AreEqual(0, coll.Count);

            words = await connection.FtsGetWordsAsync("knox", 0, 0);
            Assert.IsNotNull(words);
            Assert.AreEqual(1, words.Count);

            await connection.FtsCleanupWordsAsync();
            words = await connection.FtsGetWordsAsync("knox", 0, 0);
            Assert.IsNotNull(words);
            Assert.AreEqual(0, words.Count);

            coll = await connection.FtsGetObjectsAsync("перец пряники", false);
            Assert.AreEqual(2, coll.Count);
            Assert.IsTrue(Contains("type2", "object1", coll));
            Assert.IsTrue(Contains("type2", "object2", coll));

            Assert.AreEqual(2, await connection.FtsCountObjectsAsync("перец пряники", false));

            coll = await connection.FtsGetObjectsAsync("петя", false, null, 10, 0);
            Assert.AreEqual(1, coll.Count);

            coll = await connection.FtsGetObjectsAsync("петя", false, new string[] { "type2", "type1" }, 10, 0);
            Assert.AreEqual(1, coll.Count);

            coll = await connection.FtsGetObjectsAsync("перец пряники", true);
            Assert.AreEqual(1, coll.Count);
            Assert.IsTrue(Contains("type2", "object1", coll));

            coll = await connection.FtsGetObjectsAsync("п% х%", true);
            Assert.AreEqual(1, coll.Count);
            Assert.IsTrue(Contains("type2", "object2", coll));

            coll = await connection.FtsGetObjectsAsync("п% d%", true);
            Assert.AreEqual(0, coll.Count);

            coll = connection.FtsGetObjects("п% d%", false);
            Assert.AreEqual(4, coll.Count);
            Assert.IsTrue(Contains("type1", "object1", coll));
            Assert.IsTrue(Contains("type1", "object2", coll));
            Assert.IsTrue(Contains("type2", "object1", coll));
            Assert.IsTrue(Contains("type2", "object2", coll));

            coll = await connection.FtsGetObjectsAsync("п% d%", false, new string[] { "type1", "type2" });
            Assert.AreEqual(4, coll.Count);
            Assert.IsTrue(Contains("type1", "object1", coll));
            Assert.IsTrue(Contains("type1", "object2", coll));
            Assert.IsTrue(Contains("type2", "object1", coll));
            Assert.IsTrue(Contains("type2", "object2", coll));

            coll = await connection.FtsGetObjectsAsync("п% d%", false, new string[] { "type2" });
            Assert.AreEqual(2, coll.Count);
            Assert.IsTrue(Contains("type2", "object1", coll));
            Assert.IsTrue(Contains("type2", "object2", coll));

            await connection.FtsDeleteObjectAsync("type1", "object1");

            coll = await connection.FtsGetObjectsAsync("п% d%", false);
            Assert.IsFalse(Contains("type1", "object1", coll));

            words = await connection.FtsGetWordsAsync("f%", 0, 0);
            Assert.IsNotNull(words);
            Assert.AreEqual(3, words.Count);
            Assert.IsTrue(Contains("fox", words));
            Assert.IsTrue(Contains("foxy", words));
            Assert.IsTrue(Contains("foxes", words));

            words = await connection.FtsGetWordsAsync("f%", 1, 0);
            Assert.IsNotNull(words);
            Assert.AreEqual(1, words.Count);
            Assert.IsTrue(Contains("fox", words));

            words = await connection.FtsGetWordsAsync("moody", 0, 0);
            Assert.IsNotNull(words);
            Assert.AreEqual(1, words.Count);

            await connection.FtsCleanupWordsAsync();

            words = await connection.FtsGetWordsAsync("moody", 0, 0);
            Assert.IsNotNull(words);
            Assert.AreEqual(0, words.Count);

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

                Assert.AreEqual(2, rc.Count);
                Assert.AreEqual(1, rc[0].ID);
                Assert.AreEqual(5, rc[1].ID);
            }

            using (var query = connection.GetSelectEntitiesQuery<FtsEntity>())
            {
                query.Where.AddFtsSearch("text five", FtsQueryExtension.QueryType.AllWordsInclude, "t1");
                query.AddOrderBy(nameof(FtsEntity.ID));
                EntityCollection<FtsEntity> rc = await query.ReadAllAsync<FtsEntity>();

                Assert.AreEqual(1, rc.Count);
                Assert.AreEqual(5, rc[0].ID);
            }

            using (var query = connection.GetSelectEntitiesQuery<FtsEntity>())
            {
                query.Where.AddFtsSearch("number", FtsQueryExtension.QueryType.AllWordsInclude, "t1");
                query.AddOrderBy(nameof(FtsEntity.ID));
                EntityCollection<FtsEntity> rc = await query.ReadAllAsync<FtsEntity>();

                Assert.AreEqual(5, rc.Count);
            }

            using (var query = connection.GetSelectEntitiesQuery<FtsEntity>())
            {
                query.Where.AddFtsSearch("text", FtsQueryExtension.QueryType.AnyWordExclude, "t1");
                query.AddOrderBy(nameof(FtsEntity.ID));
                EntityCollection<FtsEntity> rc = await query.ReadAllAsync<FtsEntity>();

                Assert.AreEqual(3, rc.Count);
                foreach (var v in rc)
                    Assert.IsFalse(v.Text.Contains("text"));
            }
        }
    }
}


