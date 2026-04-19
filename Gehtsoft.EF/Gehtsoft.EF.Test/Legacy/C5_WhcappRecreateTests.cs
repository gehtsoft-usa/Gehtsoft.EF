using System;
using System.Collections.Generic;
using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.Legacy.C5Whcapp
{
    // Verbatim port of the CoinAccountant "whcapp" entity graph used to
    // reproduce defect C5 (SQLite "table wallets already exists" on the
    // second CreateEntities(true) / UpdateTables(..., Recreate) call).
    //
    // Only differences from the original model in
    // /mnt/d/develop/work.projects/Coinaccountant/.../WebApp.Dao/Models/:
    //   * Scope renamed from "whcapp" to "c5whcapp" to avoid any cross-test
    //     collision inside AllEntities.Inst / EntityFinder static state.
    //   * Namespace changed to Gehtsoft.EF.Test.Legacy.C5Whcapp.
    //   * Stripped out constructors, comment docs, and the ICompositeIndex
    //     metadata on ClientEntity (not load-bearing for the drop/recreate
    //     algorithm, and ICompositeIndexMetadata has separate call sites
    //     that could distort the repro).
    // The table names, column names, DbTypes, FK directions, Nullable/Unique
    // flags, Sorted flags, and every [ForeignKey] relation are preserved.

    #region Entities

    [Entity(Scope = "c5whcapp", Table = "wallets")]
    public sealed class WalletEntity
    {
        [AutoId]
        public int ID { get; internal set; }

        [EntityProperty(Size = 256, Nullable = false, Sorted = true, Unique = true)]
        public string Name { get; set; }
    }

    [Entity(Scope = "c5whcapp", Table = "currencies")]
    public sealed class CurrencyEntity
    {
        [AutoId]
        public int ID { get; internal set; }

        [EntityProperty(Size = 256, Nullable = false, Sorted = true)]
        public string Name { get; set; }

        [EntityProperty(Size = 256, Nullable = false, Sorted = true)]
        public string Abbreviation { get; set; }

        [EntityProperty(Sorted = true, DbType = DbType.Int32)]
        public CurrencyType CurrencyType { get; set; }

        [EntityProperty(Size = 256, Nullable = false, Sorted = true)]
        public string ExternalID { get; set; }

        [EntityProperty(DbType = DbType.Boolean, Sorted = true)]
        public bool NoProvider { get; set; }
    }

    public enum CurrencyType
    {
        FIAT = 0,
        CRYPTO = 1,
    }

    [Entity(Scope = "c5whcapp", Table = "clients")]
    public class ClientEntity
    {
        [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
        public int ID { get; internal set; }

        [EntityProperty(Field = "internalid", DbType = DbType.String, Size = 128, Nullable = true)]
        public string InternalID { get; set; }

        [EntityProperty(Field = "name", DbType = DbType.String, Size = 256, Nullable = false, Unique = true)]
        public string Name { get; set; }

        [EntityProperty(Field = "taxid", DbType = DbType.String, Size = 128, Nullable = true)]
        public string TaxID { get; set; }

        [EntityProperty(Field = "advisor", DbType = DbType.String, Size = 256, Nullable = true)]
        public string Advisor { get; set; }

        [EntityProperty(Field = "suspended", DbType = DbType.Boolean, Sorted = true, Nullable = false)]
        public bool Suspended { get; set; }

        [EntityProperty(Field = "StatusChangedDate", DbType = DbType.DateTime, Nullable = false)]
        public DateTime StatusChangedDate { get; set; }
    }

    [Entity(Scope = "c5whcapp", Table = "bookingcodes")]
    public class BookingCodeEntity
    {
        [AutoId]
        public int ID { get; internal set; }

        [EntityProperty(Size = 256, Nullable = false, Sorted = true, Unique = true)]
        public string Name { get; set; }
    }

    [OnEntityCreate(typeof(C5_WhcappSeed), nameof(C5_WhcappSeed.CreateUsers))]
    [Entity(Scope = "c5whcapp", Table = "users")]
    public class UserEntity
    {
        [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
        public int ID { get; internal set; }

        [EntityProperty(Field = "username", DbType = DbType.String, Size = 64, Sorted = true, Unique = true)]
        public string Username { get; set; }

        [EntityProperty(Field = "passwordHash", DbType = DbType.String, Size = 64)]
        public string PasswordHash { get; set; }

        [EntityProperty(Field = "suspended", DbType = DbType.Boolean, Sorted = true)]
        public bool Suspended { get; set; }

        [EntityProperty(Field = "superuser", DbType = DbType.Boolean, Sorted = true)]
        public bool Superuser { get; set; }
    }

    [Entity(Scope = "c5whcapp", Table = "clientwallets")]
    public class WalletMapEntity
    {
        [AutoId]
        public int ID { get; internal set; }

        [EntityProperty(Size = 16, Nullable = false, Sorted = true)]
        public string Code { get; set; }

        [EntityProperty(Field = "clientid", ForeignKey = true)]
        public ClientEntity Client { get; set; }

        [EntityProperty(Field = "walletid", ForeignKey = true)]
        public WalletEntity Wallet { get; set; }

        [EntityProperty(Field = "ignored", DbType = DbType.Boolean, Sorted = true, Nullable = false)]
        public bool Ignored { get; set; }
    }

    [Entity(Scope = "c5whcapp", Table = "clientcurrencies")]
    public class CurrencyMapEntity
    {
        [AutoId]
        public int ID { get; internal set; }

        [EntityProperty(Size = 64, Nullable = false, Sorted = true)]
        public string Code { get; set; }

        [EntityProperty(Field = "clientid", ForeignKey = true)]
        public ClientEntity Client { get; set; }

        [EntityProperty(Field = "currencyid", ForeignKey = true)]
        public CurrencyEntity Currency { get; set; }
    }

    [Entity(Scope = "c5whcapp", Table = "clientbookingcodes")]
    public class BookingCodeMapEntity
    {
        [AutoId]
        public int ID { get; internal set; }

        [EntityProperty(Size = 64, Nullable = false, Sorted = true)]
        public string Code { get; set; }

        [EntityProperty(Field = "clientid", ForeignKey = true)]
        public ClientEntity Client { get; set; }

        [EntityProperty(Field = "bookingcodeid", ForeignKey = true)]
        public BookingCodeEntity BookingCode { get; set; }
    }

    [Entity(Scope = "c5whcapp", Table = "rates")]
    public class RateEntity
    {
        [AutoId]
        public int ID { get; internal set; }

        [EntityProperty(Nullable = false)]
        public double Open { get; set; }

        [EntityProperty(Nullable = false)]
        public double High { get; set; }

        [EntityProperty(Nullable = false)]
        public double Low { get; set; }

        [EntityProperty(Nullable = false)]
        public double Close { get; set; }

        [EntityProperty(Nullable = false)]
        public double Volume { get; set; }

        [EntityProperty(Nullable = false, Sorted = true)]
        public DateTime Date { get; set; }

        [EntityProperty(Field = "currencyid", ForeignKey = true, Sorted = true)]
        public CurrencyEntity Currency { get; set; }

        [EntityProperty(Field = "ratetoid", ForeignKey = true)]
        public CurrencyEntity RateTo { get; set; }
    }

    [Entity(Scope = "c5whcapp", Table = "accountingbalances")]
    public class AccountingBalanceEntity
    {
        [AutoId]
        public int ID { get; internal set; }

        [EntityProperty(Sorted = true, ForeignKey = true, Field = "walletid")]
        public WalletEntity Wallet { get; set; }

        [EntityProperty(Sorted = true, ForeignKey = true, Field = "currencyid")]
        public CurrencyEntity Currency { get; set; }

        [EntityProperty(Sorted = true, ForeignKey = true, Field = "clientid")]
        public ClientEntity Client { get; set; }

        [EntityProperty(Field = "date", DbType = DbType.DateTime, Nullable = false)]
        public DateTime Datum { get; set; }

        [EntityProperty(Field = "value", DbType = DbType.Decimal, Nullable = false)]
        public decimal Value { get; set; }
    }

    [Entity(Scope = "c5whcapp", Table = "transactions")]
    public class ClientTransactionEntity
    {
        [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
        public int ID { get; private set; }

        [EntityProperty(Field = "clientid", ForeignKey = true, Nullable = false)]
        public ClientEntity Client { get; set; }

        [EntityProperty(Field = "TransactionDate", DbType = DbType.DateTime, Nullable = false)]
        public DateTime TransactionDate { get; set; }

        [EntityProperty(Field = "transactiontype", DbType = DbType.String, Size = 256, Nullable = false)]
        public string Type { get; set; }

        [EntityProperty(DbType = DbType.Double, Nullable = true)]
        public double? BoughtQuantity { get; set; }

        [EntityProperty(Field = "boughtcurrencyid", ForeignKey = true, Nullable = true)]
        public CurrencyEntity BoughtCurrency { get; set; }

        [EntityProperty(DbType = DbType.Double, Nullable = true)]
        public double? SoldQuantity { get; set; }

        [EntityProperty(Field = "soldcurrencyid", ForeignKey = true, Nullable = true)]
        public CurrencyEntity SoldCurrency { get; set; }

        [EntityProperty(DbType = DbType.Double, Nullable = true)]
        public double? FeeQuantity { get; set; }

        [EntityProperty(Field = "feecurrencyid", ForeignKey = true, Nullable = true)]
        public CurrencyEntity FeeCurrency { get; set; }

        [EntityProperty(DbType = DbType.String, Size = 256, Nullable = true)]
        public string Classification { get; set; }

        [EntityProperty(Field = "walletid", ForeignKey = true, Nullable = false)]
        public WalletEntity Wallet { get; set; }

        [EntityProperty(Field = "taxid", DbType = DbType.String, Size = 256, Nullable = true)]
        public string TxId { get; set; }

        [EntityProperty(Field = "tranasactionid", DbType = DbType.Int64, Nullable = false)]
        public long TransactionId { get; set; }
    }

    #endregion

    // Stand-in for UserInitialization.CreateUsers in WebApp.Dao.
    // Same shape: a static method invoked by the OnEntityCreate hook on
    // UserEntity, inserting two seed users.
    internal static class C5_WhcappSeed
    {
        public static void CreateUsers(SqlDbConnection connection)
        {
            var internalUser = new UserEntity
            {
                Username = "systemuser",
                PasswordHash = "hash1",
                Suspended = false,
                Superuser = true,
            };
            using (var query = connection.GetInsertEntityQuery(typeof(UserEntity)))
                query.Execute(internalUser);

            var adminUser = new UserEntity
            {
                Username = "admin",
                PasswordHash = "hash2",
                Suspended = false,
                Superuser = false,
            };
            using (var query = connection.GetInsertEntityQuery(typeof(UserEntity)))
                query.Execute(adminUser);
        }
    }

    public class C5_WhcappRecreateTests
    {
        private static int Count<T>(SqlDbConnection connection)
            where T : class, new()
        {
            using var query = connection.GetSelectEntitiesCountQuery<T>();
            return query.RowCount;
        }

        private static void CreateUser(SqlDbConnection connection)
        {
            var user = new UserEntity
            {
                PasswordHash = "hashX",
                Superuser = false,
                Username = "test",
                Suspended = false,
            };
            using var query = connection.GetInsertEntityQuery<UserEntity>();
            query.Execute(user);
        }

        // Exact port of CoinAccountant's WebApp.Dao.UnitTest.CreateEntityTest.CheckRecreate.
        [Fact]
        public void CheckRecreate_Whcapp()
        {
            using var connection = SqliteDbConnectionFactory.CreateMemory();
            var controller = new CreateEntityController(typeof(UserEntity), "c5whcapp");

            // 1st Recreate — OK
            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);
            Count<UserEntity>(connection).Should().Be(2);

            CreateUser(connection);
            Count<UserEntity>(connection).Should().Be(3);

            // 2nd Recreate — in CoinAccountant this throws
            //   SqliteException : 'table wallets already exists'
            ((Action)(() =>
                controller.UpdateTables(connection, CreateEntityController.UpdateMode.Recreate)))
                .Should().NotThrow();

            Count<UserEntity>(connection).Should().Be(2);
        }

        // Diagnostic: on the second Recreate call, each non-view table must
        // see a Drop emitted before its Create. If the bug fires, we'll spot
        // a Create without a preceding Drop for the offending table.
        [Fact]
        public void SecondRecreate_Emits_Drop_Before_Create_For_Every_Table_Whcapp()
        {
            using var connection = SqliteDbConnectionFactory.CreateMemory();
            var controller = new CreateEntityController(typeof(UserEntity), "c5whcapp");
            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);

            var order = new List<string>();
            controller.OnAction += (_, e) => order.Add($"{e.EventAction}:{e.Table}");

            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);

            var expectedTables = new[]
            {
                "wallets", "currencies", "clients", "bookingcodes", "users",
                "clientwallets", "clientcurrencies", "clientbookingcodes",
                "rates", "accountingbalances", "transactions",
            };

            foreach (var t in expectedTables)
            {
                var dropIdx = order.IndexOf($"Drop:{t}");
                var createIdx = order.IndexOf($"Create:{t}");
                dropIdx.Should().BeGreaterThanOrEqualTo(0, $"expected Drop:{t} to be emitted");
                createIdx.Should().BeGreaterThan(dropIdx, $"expected Drop before Create for {t}");
            }
        }
    }
}
