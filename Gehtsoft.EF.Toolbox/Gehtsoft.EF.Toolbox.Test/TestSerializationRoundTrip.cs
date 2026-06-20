using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Serialization.IO;
using Gehtsoft.EF.Serialization.IO.Binary;
using Gehtsoft.EF.Serialization.IO.Db;
using Gehtsoft.EF.Serialization.IO.Json;
using Gehtsoft.EF.Serialization.IO.Xml;
using AwesomeAssertions;
using Xunit;

namespace Gehtsoft.EF.Toolbox.Test
{
    /// <summary>
    /// Integration coverage for Gehtsoft.EF.Serialization.
    ///
    /// The entity graph below intentionally exercises every relationship style the
    /// serializers must handle:
    ///   * aggregation        Account [1] -> [*] Transaction
    ///   * plain reference     Account [*] -> [1] AccountType
    ///   * self reference      AccountType [0..1 parent] -> [*] AccountType
    /// and every primitive type that round-trips through BOTH storage media
    /// (string, bool, short, int, double, decimal/money, DateTime with time,
    /// DateTime date-only, enum and byte[] blob).
    ///
    /// The full round trip is verified for every storage format the library ships:
    /// the relational DB writer/reader (against in-memory SQLite), XML, Binary and JSON.
    /// All non-DB formats identify entity types by EF scope + table name and resolve them
    /// against the supplied EntityFinder.EntityTypeInfo[] (as DbEntityReader does).
    /// </summary>
    public class TestSerializationRoundTrip
    {
        private const string Scope = "ser_roundtrip";

        public enum AccountKind
        {
            Asset = 0,
            Liability = 1,
            Equity = 2,
            Income = 3,
            Expense = 4,
        }

        [Entity(Scope = Scope, Table = "ser_accounttype")]
        public class AccountType
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Size = 64, Sorted = true)]
            public string Name { get; set; }

            // self reference: a type may have a parent type
            [EntityProperty(ForeignKey = true, Nullable = true)]
            public AccountType Parent { get; set; }
        }

        [Entity(Scope = Scope, Table = "ser_account")]
        public class Account
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            // reference: many accounts -> one account type
            [EntityProperty(ForeignKey = true, Nullable = true)]
            public AccountType Type { get; set; }

            [EntityProperty(Size = 128)]
            public string Name { get; set; }

            [EntityProperty]
            public bool Active { get; set; }

            // short / long / float now auto-detect to Int16 / Int64 / Single
            [EntityProperty]
            public short Priority { get; set; }

            [EntityProperty]
            public int Number { get; set; }

            [EntityProperty]
            public long Serial { get; set; }

            [EntityProperty]
            public float Ratio { get; set; }

            // reference identifier carried as a Guid
            [EntityProperty(Nullable = true)]
            public Guid? ExternalId { get; set; }

            [EntityProperty(DbType = DbType.Double)]
            public double InterestRate { get; set; }

            [EntityProperty(DbType = DbType.Decimal, Size = 18, Precision = 4)]
            public decimal Balance { get; set; }

            [EntityProperty(DbType = DbType.DateTime)]
            public DateTime OpenedAt { get; set; }

            [EntityProperty(DbType = DbType.Date, Nullable = true)]
            public DateTime? ClosedOn { get; set; }

            [EntityProperty(DbType = DbType.Int32)]
            public AccountKind Kind { get; set; }

            [EntityProperty(DbType = DbType.Binary, Size = 256, Nullable = true)]
            public byte[] Signature { get; set; }
        }

        [Entity(Scope = Scope, Table = "ser_transaction")]
        public class Transaction
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            // aggregation: one account -> many transactions
            [EntityProperty(ForeignKey = true)]
            public Account Account { get; set; }

            [EntityProperty(DbType = DbType.Decimal, Size = 18, Precision = 4)]
            public decimal Amount { get; set; }

            [EntityProperty(DbType = DbType.DateTime)]
            public DateTime PostedAt { get; set; }

            [EntityProperty(Size = 256, Nullable = true)]
            public string Memo { get; set; }
        }

        private sealed class SeedData
        {
            public List<AccountType> Types { get; } = new List<AccountType>();
            public List<Account> Accounts { get; } = new List<Account>();
            public List<Transaction> Transactions { get; } = new List<Transaction>();
        }

        private static readonly Assembly[] Assemblies = { typeof(TestSerializationRoundTrip).Assembly };

        private static EntityFinder.EntityTypeInfo[] DiscoverEntities()
            => EntityFinder.FindEntities(Assemblies, Scope, false);

        private static void CreateSchema(SqlDbConnection connection)
        {
            using (var q = connection.GetCreateEntityQuery<AccountType>())
                q.Execute();
            using (var q = connection.GetCreateEntityQuery<Account>())
                q.Execute();
            using (var q = connection.GetCreateEntityQuery<Transaction>())
                q.Execute();
        }

        private static SeedData Seed(SqlDbConnection connection)
        {
            var data = new SeedData();

            // account types - including a self-referencing tree (root -> child)
            var assets = new AccountType { Name = "Assets", Parent = null };
            var liabilities = new AccountType { Name = "Liabilities", Parent = null };
            using (var q = connection.GetInsertEntityQuery<AccountType>())
            {
                q.Execute(assets);
                q.Execute(liabilities);
            }
            // child of "Assets" - inserted after its parent so the FK resolves
            var cash = new AccountType { Name = "Cash", Parent = assets };
            using (var q = connection.GetInsertEntityQuery<AccountType>())
                q.Execute(cash);
            data.Types.AddRange(new[] { assets, liabilities, cash });

            // accounts - one fully populated, one with the nullable fields left empty
            var checking = new Account
            {
                Type = cash,
                Name = "Checking",
                Active = true,
                Priority = 7,
                Number = 1001,
                Serial = 9_000_000_001L,
                Ratio = 0.5f,
                ExternalId = new Guid("11112222-3333-4444-5555-666677778888"),
                InterestRate = 0.0125,
                Balance = 1234.5678m,
                OpenedAt = new DateTime(2024, 3, 15, 9, 30, 0),
                ClosedOn = new DateTime(2025, 12, 31),
                Kind = AccountKind.Asset,
                Signature = new byte[] { 0x00, 0x01, 0x02, 0xFE, 0xFF },
            };
            var loan = new Account
            {
                Type = liabilities,
                Name = "Mortgage",
                Active = false,
                Priority = -3,
                Number = 2002,
                Serial = -5_000_000_000L,
                Ratio = 0.25f,
                ExternalId = null,
                InterestRate = 0.0699,
                Balance = -250000.0000m,
                OpenedAt = new DateTime(2020, 1, 1, 0, 0, 0),
                ClosedOn = null,
                Kind = AccountKind.Liability,
                Signature = null,
            };
            using (var q = connection.GetInsertEntityQuery<Account>())
            {
                q.Execute(checking);
                q.Execute(loan);
            }
            data.Accounts.AddRange(new[] { checking, loan });

            // transactions - several per account (the [1]->[*] aggregation)
            var transactions = new[]
            {
                new Transaction { Account = checking, Amount = 100.00m, PostedAt = new DateTime(2024, 4, 1, 12, 0, 0), Memo = "deposit" },
                new Transaction { Account = checking, Amount = -42.50m, PostedAt = new DateTime(2024, 4, 2, 8, 15, 0), Memo = "groceries" },
                new Transaction { Account = checking, Amount = -1000.00m, PostedAt = new DateTime(2024, 4, 3, 18, 45, 0), Memo = null },
                new Transaction { Account = loan, Amount = 1500.00m, PostedAt = new DateTime(2024, 4, 5, 0, 0, 0), Memo = "payment" },
            };
            using (var q = connection.GetInsertEntityQuery<Transaction>())
            {
                foreach (var t in transactions)
                    q.Execute(t);
            }
            data.Transactions.AddRange(transactions);

            return data;
        }

        private static List<T> ReadAll<T>(SqlDbConnection connection) where T : class
        {
            var list = new List<T>();
            using (var q = connection.GetSelectEntitiesQuery<T>())
            {
                q.Execute();
                while (true)
                {
                    var e = q.ReadOne<T>();
                    if (e == null)
                        break;
                    list.Add(e);
                }
            }
            return list;
        }

        private static string SerializeToXml(SqlDbConnection source)
        {
            var sb = new StringBuilder();
            using (var writer = new XmlEntityWriter(sb))
            {
                var reader = new DbEntityReader(DiscoverEntities(), source, null);
                reader.OnTypeStarted += t => writer.Start(t);
                reader.OnEntity += e => writer.Write(e);
                reader.Scan();
            }
            return sb.ToString();
        }

        private static void DeserializeXmlToDb(string xml, SqlDbConnection target)
        {
            using (var writer = new DbEntityWriter(target))
            {
                var reader = new XmlEntityReader(DiscoverEntities(), xml);
                reader.OnTypeStarted += t => writer.Start(t);
                reader.OnEntity += e => writer.Write(e);
                reader.Scan();
            }
        }

        private static byte[] SerializeToBinary(SqlDbConnection source)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryEntityWriter(stream))
                {
                    var reader = new DbEntityReader(DiscoverEntities(), source, null);
                    reader.OnTypeStarted += t => writer.Start(t);
                    reader.OnEntity += e => writer.Write(e);
                    reader.Scan();
                }
                return stream.ToArray();
            }
        }

        private static void DeserializeBinaryToDb(byte[] payload, SqlDbConnection target)
        {
            using (var stream = new MemoryStream(payload))
            using (var writer = new DbEntityWriter(target))
            {
                var reader = new BinaryEntityReader(DiscoverEntities(), stream);
                reader.OnTypeStarted += t => writer.Start(t);
                reader.OnEntity += e => writer.Write(e);
                reader.Scan();
            }
        }

        private static string SerializeToJson(SqlDbConnection source)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new JsonEntityWriter(stream))
                {
                    var reader = new DbEntityReader(DiscoverEntities(), source, null);
                    reader.OnTypeStarted += t => writer.Start(t);
                    reader.OnEntity += e => writer.Write(e);
                    reader.Scan();
                }
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static void DeserializeJsonToDb(string json, SqlDbConnection target)
        {
            using (var writer = new DbEntityWriter(target))
            {
                var reader = new JsonEntityReader(DiscoverEntities(), json);
                reader.OnTypeStarted += t => writer.Start(t);
                reader.OnEntity += e => writer.Write(e);
                reader.Scan();
            }
        }

        private static void CopyDbToDb(SqlDbConnection source, SqlDbConnection target)
        {
            using (var writer = new DbEntityWriter(target))
            {
                var reader = new DbEntityReader(DiscoverEntities(), source, null);
                reader.OnTypeStarted += t => writer.Start(t);
                reader.OnEntity += e => writer.Write(e);
                reader.Scan();
            }
        }

        private static void AssertGraphMatches(SeedData expected, SqlDbConnection target)
        {
            var types = ReadAll<AccountType>(target);
            var accounts = ReadAll<Account>(target);
            var transactions = ReadAll<Transaction>(target);

            types.Should().HaveCount(expected.Types.Count);
            accounts.Should().HaveCount(expected.Accounts.Count);
            transactions.Should().HaveCount(expected.Transactions.Count);

            foreach (var exp in expected.Types)
            {
                var act = types.Single(x => x.ID == exp.ID);
                act.Name.Should().Be(exp.Name);
                (act.Parent?.ID).Should().Be(exp.Parent?.ID);
            }

            foreach (var exp in expected.Accounts)
            {
                var act = accounts.Single(x => x.ID == exp.ID);
                (act.Type?.ID).Should().Be(exp.Type?.ID);
                act.Name.Should().Be(exp.Name);
                act.Active.Should().Be(exp.Active);
                act.Priority.Should().Be(exp.Priority);
                act.Number.Should().Be(exp.Number);
                act.Serial.Should().Be(exp.Serial);
                act.Ratio.Should().Be(exp.Ratio);
                act.ExternalId.Should().Be(exp.ExternalId);
                act.InterestRate.Should().BeApproximately(exp.InterestRate, 1e-9);
                act.Balance.Should().Be(exp.Balance);
                act.OpenedAt.Should().Be(exp.OpenedAt);
                act.ClosedOn.Should().Be(exp.ClosedOn);
                act.Kind.Should().Be(exp.Kind);
                if (exp.Signature == null)
                    act.Signature.Should().BeNull();
                else
                    act.Signature.Should().Equal(exp.Signature);
            }

            foreach (var exp in expected.Transactions)
            {
                var act = transactions.Single(x => x.ID == exp.ID);
                act.Account.Should().NotBeNull();
                act.Account.ID.Should().Be(exp.Account.ID);
                act.Amount.Should().Be(exp.Amount);
                act.PostedAt.Should().Be(exp.PostedAt);
                act.Memo.Should().Be(exp.Memo);
            }
        }

        [Fact]
        public void RoundTrip_Db_To_Db()
        {
            using var source = SqliteDbConnectionFactory.CreateMemory();
            CreateSchema(source);
            var data = Seed(source);

            using var target = SqliteDbConnectionFactory.CreateMemory();
            CreateSchema(target);

            CopyDbToDb(source, target);

            AssertGraphMatches(data, target);
        }

        [Fact]
        public void RoundTrip_Db_To_Xml_To_Db()
        {
            using var source = SqliteDbConnectionFactory.CreateMemory();
            CreateSchema(source);
            var data = Seed(source);

            string xml = SerializeToXml(source);

            using var target = SqliteDbConnectionFactory.CreateMemory();
            CreateSchema(target);

            DeserializeXmlToDb(xml, target);

            AssertGraphMatches(data, target);
        }

        [Fact]
        public void RoundTrip_Db_To_Binary_To_Db()
        {
            using var source = SqliteDbConnectionFactory.CreateMemory();
            CreateSchema(source);
            var data = Seed(source);

            byte[] payload = SerializeToBinary(source);
            payload.Should().NotBeEmpty();

            using var target = SqliteDbConnectionFactory.CreateMemory();
            CreateSchema(target);

            DeserializeBinaryToDb(payload, target);

            AssertGraphMatches(data, target);
        }

        [Fact]
        public void RoundTrip_Db_To_Json_To_Db()
        {
            using var source = SqliteDbConnectionFactory.CreateMemory();
            CreateSchema(source);
            var data = Seed(source);

            string json = SerializeToJson(source);

            // sanity: it must parse as JSON
            using (var doc = System.Text.Json.JsonDocument.Parse(json))
                doc.RootElement.GetProperty("es").GetArrayLength().Should().Be(3);

            using var target = SqliteDbConnectionFactory.CreateMemory();
            CreateSchema(target);

            DeserializeJsonToDb(json, target);

            AssertGraphMatches(data, target);
        }

        [Fact]
        public void Xml_Document_Is_WellFormed_And_Complete()
        {
            using var source = SqliteDbConnectionFactory.CreateMemory();
            CreateSchema(source);
            var data = Seed(source);

            string xml = SerializeToXml(source);

            xml.Should().NotBeNullOrEmpty();
            // it must parse as XML
            var doc = new System.Xml.XmlDocument();
            doc.Invoking(d => d.LoadXml(xml)).Should().NotThrow();

            // one <t> type element per entity type, one <e> entity element per row
            int typeElements = System.Text.RegularExpressions.Regex.Matches(xml, "<t ").Count;
            typeElements.Should().Be(3);

            int entityElements = System.Text.RegularExpressions.Regex.Matches(xml, "<e>").Count
                + System.Text.RegularExpressions.Regex.Matches(xml, "<e />").Count;
            entityElements.Should().Be(data.Types.Count + data.Accounts.Count + data.Transactions.Count);
        }

        // long, Guid and float now round-trip through TextFormatter (the XML/Blob path),
        // matching the set the DB path supports.
        [Theory]
        [InlineData((long)0)]
        [InlineData(9_000_000_001L)]
        [InlineData(-5_000_000_000L)]
        [InlineData(long.MaxValue)]
        [InlineData(long.MinValue)]
        public void TextFormatter_RoundTrips_Long(long value)
        {
            TextFormatter.Format(value, out string formatted, out string type).Should().BeTrue();
            type.Should().Be("q");
            TextFormatter.ParseAndConvert<long>(type, formatted).Should().Be(value);
        }

        [Fact]
        public void TextFormatter_RoundTrips_Guid()
        {
            var value = new Guid("11112222-3333-4444-5555-666677778888");
            TextFormatter.Format(value, out string formatted, out string type).Should().BeTrue();
            type.Should().Be("g");
            TextFormatter.ParseAndConvert<Guid>(type, formatted).Should().Be(value);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(0.5f)]
        [InlineData(-0.25f)]
        [InlineData(3.5f)]
        public void TextFormatter_RoundTrips_Float(float value)
        {
            TextFormatter.Format(value, out string formatted, out string type).Should().BeTrue();
            type.Should().Be("f");
            TextFormatter.ParseAndConvert<float>(type, formatted).Should().Be(value);
        }

        public static IEnumerable<object[]> BinaryScalars()
        {
            yield return new object[] { null };
            yield return new object[] { "hello" };
            yield return new object[] { (short)-12345 };
            yield return new object[] { 1234567 };
            yield return new object[] { 9_000_000_001L };
            yield return new object[] { 0.5f };
            yield return new object[] { 3.14159d };
            yield return new object[] { true };
            yield return new object[] { new DateTime(2024, 3, 15, 9, 30, 0, DateTimeKind.Utc) };
            yield return new object[] { 1234.5678m };
            yield return new object[] { new Guid("11112222-3333-4444-5555-666677778888") };
            yield return new object[] { new byte[] { 0x00, 0x01, 0xFE, 0xFF } };
        }

        [Theory]
        [MemberData(nameof(BinaryScalars))]
        public void BinaryFormatter_RoundTrips_Scalars(object value)
        {
            byte[] payload;
            using (var stream = new MemoryStream())
            {
                using (var bw = new BinaryWriter(stream, Encoding.UTF8, true))
                    BinaryFormatter.Write(bw, value);
                payload = stream.ToArray();
            }

            object read;
            using (var stream = new MemoryStream(payload))
            using (var br = new BinaryReader(stream, Encoding.UTF8, true))
                read = BinaryFormatter.Read(br);

            if (value is byte[] expectedBytes)
                ((byte[])read).Should().Equal(expectedBytes);
            else
                read.Should().Be(value);
        }
    }
}
