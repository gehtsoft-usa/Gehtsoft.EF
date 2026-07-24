using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Gehtsoft.EF.Db.SqlDb.Sql.Test
{
    /// <summary>
    /// Runs the INSERT/UPDATE/DELETE SQL-DSL runners against every enabled connection in the shared
    /// Configuration.json (the same servers the main suite uses). SQLite alone cannot reach the
    /// driver-specific runner paths - notably the autoincrement-value-returned-as-an-output-parameter style
    /// (Oracle) versus the returned-through-the-reader style (SQLite/PostgreSQL/MySQL) and the MSSQL
    /// multiple-identity handling. A connection that is not reachable is skipped, so the suite stays green
    /// when a server is down.
    /// </summary>
    public sealed class MultiDriverRunnerRun
    {
        public static IEnumerable<object[]> Connections()
        {
            IConfiguration config = new ConfigurationBuilder().AddJsonFile("Configuration.json", optional: true).Build();
            foreach (IConfigurationSection child in config.GetSection("sqlConnections").GetChildren())
            {
                string driver = child["driver"];
                string connectionString = child["connectionString"];
                if (child["enabled"] == "true" && !string.IsNullOrEmpty(driver) && !string.IsNullOrEmpty(connectionString))
                    yield return new object[] { child.Key, driver, connectionString };
            }
        }

        [Theory]
        [MemberData(nameof(Connections))]
        public void InsertUpdateDelete_AcrossDrivers(string name, string driver, string connectionString)
        {
            ISqlDbConnectionFactory factory;
            SqlDbConnection connection;
            try
            {
                factory = new SqlDbUniversalConnectionFactory(driver, connectionString);
                connection = factory.GetConnection();
            }
            catch (Exception e)
            {
                Assert.Skip($"connection '{name}' ({driver}) is not reachable: {e.Message}");
                return;
            }

            try
            {
                // schema only (no seed data): every driver starts with an empty table and a fresh
                // autoincrement sequence, so the INSERT returns id 1 uniformly - no per-driver sequence
                // realignment, and the Oracle "value returned as an output parameter" path is exercised.
                Snapshot snapshot = new Snapshot();
                snapshot.CreateTablesAsync(connection).ConfigureAwait(true).GetAwaiter().GetResult();

                EntityFinder.EntityTypeInfo[] entities = EntityFinder.FindEntities(new[] { typeof(Snapshot).Assembly }, "northwind", false);
                SqlCodeDomBuilder domBuilder = new SqlCodeDomBuilder();
                domBuilder.Build(entities, "entities");
                SqlCodeDomEnvironment env = domBuilder.NewEnvironment(connection);

                // INSERT - exercises the autoincrement-return path for this driver's style
                dynamic inserted = env.Parse("test",
                    "INSERT INTO Supplier (CompanyName, ContactName, ContactTitle, Address, City, PostalCode, Country) " +
                    "VALUES ('MultiDrv Co', 'Tester', 'QA', 'Addr 1', 'Town', '00000', 'Testland')")(null);
                long id = Convert.ToInt64(inserted[0].LastInsertedId);
                id.Should().BeGreaterThan(0);

                // UPDATE + verify
                env.Parse("test", $"UPDATE Supplier SET City='Town2' WHERE SupplierID={id}")(null);
                dynamic afterUpdate = env.Parse("test", $"SELECT City FROM Supplier WHERE SupplierID={id}")(null);
                ((string)afterUpdate[0].City).Should().Be("Town2");

                // DELETE + verify
                env.Parse("test", $"DELETE FROM Supplier WHERE SupplierID={id}")(null);
                dynamic afterDelete = env.Parse("test", $"SELECT COUNT(*) AS Total FROM Supplier WHERE SupplierID={id}")(null);
                ((int)afterDelete[0].Total).Should().Be(0);
            }
            finally
            {
                if (factory.NeedDispose)
                    connection.Dispose();
            }
        }
    }
}
