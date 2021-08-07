using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.MssqlDb;
using Gehtsoft.EF.Db.MysqlDb;
using Gehtsoft.EF.Db.OracleDb;
using Gehtsoft.EF.Db.PostgresDb;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqliteDb;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb.Factory
{
    public class UnversalConfigurationFactory
    {
        [Theory]
        [InlineData("mssql", typeof(MssqlDbConnection))]
        [InlineData("mysql", typeof(MysqlDbConnection))]
        [InlineData("npgsql", typeof(PostgresDbConnection))]
        [InlineData("sqlite", typeof(SqliteDbConnection))]
        [InlineData("oracle", typeof(OracleDbConnection))]
        public void GetConnection(string driver, Type expectedConnectionType)
        {
            using var connection = UniversalSqlDbFactory.Create(driver, TestConfiguration.Instance["connections:" + driver]);
            connection.Should().NotBeNull();
            connection.Should().BeOfType(expectedConnectionType);
            connection.ConnectionType.Should().Match(v => driver.Equals(v, StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData("mssql", typeof(MssqlDbConnection))]
        [InlineData("mysql", typeof(MysqlDbConnection))]
        [InlineData("npgsql", typeof(PostgresDbConnection))]
        [InlineData("sqlite", typeof(SqliteDbConnection))]
        [InlineData("oracle", typeof(OracleDbConnection))]
        public async Task GetConnectionAsync(string driver, Type expectedConnectionType)
        {
            using var connection = await UniversalSqlDbFactory.CreateAsync(driver, TestConfiguration.Instance["connections:" + driver]);
            connection.Should().NotBeNull();
            connection.Should().BeOfType(expectedConnectionType);
        }
    }
}
