using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Legacy.C4SortedFk
{
    // Reproduction test for defect C4 from the CoinAccountant repo
    // (see work.projects/Coinaccountant/.../C4.md):
    //
    // An entity property declared as
    //   [EntityProperty(ForeignKey = true, Sorted = true)]
    // is expected to produce a CREATE INDEX statement for the FK column
    // when CreateEntityController.UpdateTables builds the table. On SQLite
    // this index was generated correctly. On PostgreSQL the reporter saw
    // the index missing, which forced a hand-written
    //   CREATE INDEX IF NOT EXISTS rates_currencyid ON public.rates (currencyid)
    // workaround in DaoConnection.UpdateDB().
    //
    // The entity shape here mirrors RateEntity / CurrencyEntity from the
    // original report (same field names "currencyid" / "ratetoid", same
    // Sorted/ForeignKey combinations). The scope is "c4ratesfk" to keep
    // the graph isolated from the C5 "c5whcapp" graph which declares the
    // same business types under a different scope name.

    [Entity(Scope = "c4ratesfk", Table = "c4_currencies")]
    public sealed class CurrencyEntity
    {
        [AutoId]
        public int ID { get; internal set; }

        [EntityProperty(Size = 256, Nullable = false)]
        public string Name { get; set; }
    }

    [Entity(Scope = "c4ratesfk", Table = "c4_rates")]
    public sealed class RateEntity
    {
        [AutoId]
        public int ID { get; internal set; }

        [EntityProperty(Nullable = false)]
        public double Value { get; set; }

        // The FK under test: Sorted = true must yield an index on currencyid.
        [EntityProperty(Field = "currencyid", ForeignKey = true, Sorted = true)]
        public CurrencyEntity Currency { get; set; }

        // Companion FK with no Sorted flag - kept for parity with the
        // original RateEntity. Not asserted on, since the driver decides
        // whether an unsorted FK is indexed automatically.
        [EntityProperty(Field = "ratetoid", ForeignKey = true)]
        public CurrencyEntity RateTo { get; set; }
    }

    public class C4_SortedFkIndexTests : IClassFixture<C4_SortedFkIndexTests.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
            protected override void ConfigureConnection(SqlDbConnection connection)
            {
                Drop(connection);
                base.ConfigureConnection(connection);
            }

            protected override void TearDownConnection(SqlDbConnection connection)
            {
                Drop(connection);
                base.TearDownConnection(connection);
            }

            private static void Drop(SqlDbConnection connection)
            {
                using (var query = connection.GetDropEntityQuery<RateEntity>())
                    query.Execute();

                using (var query = connection.GetDropEntityQuery<CurrencyEntity>())
                    query.Execute();
            }
        }

        private readonly Fixture mFixture;

        public C4_SortedFkIndexTests(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = "")
            => SqlConnectionSources.SqlConnectionNames(flags);

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void UpdateTables_SortedForeignKey_CreatesIndex(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            var controller = new CreateEntityController(typeof(RateEntity), "c4ratesfk");
            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);

            connection.DoesObjectExist("c4_rates", null, "table").Should().BeTrue();
            connection.DoesObjectExist("c4_rates", "currencyid", "column").Should().BeTrue();

            // The core C4 assertion: an index named c4_rates_currencyid must
            // exist after UpdateTables because the FK declares Sorted = true.
            connection.DoesObjectExist("c4_rates", "currencyid", "index")
                .Should().BeTrue(
                    "[EntityProperty(ForeignKey = true, Sorted = true)] must " +
                    "generate an index on the FK column on {0}", connectionName);
        }
    }
}
