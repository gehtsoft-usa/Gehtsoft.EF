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
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb.Factory
{
    public class UnversalConfigurationFactory
    {
        [Theory]
        [MemberData(nameof(Connections), parameters: "")]
        public void GetConnection(string connectionName, Type expectedType)
        {
            var config = AppConfiguration.Instance.GetSqlConnection(connectionName);
            using var connection = UniversalSqlDbFactory.Create(config.Driver, config.ConnectionString);
            connection.Should().NotBeNull();
            connection.Should().BeOfType(expectedType);
            connection.ConnectionType.Should().Match(v => config.Driver.Equals(v, StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [MemberData(nameof(Connections), parameters: "")]
        public async Task GetConnectionAsync(string connectionName, Type expectedType)
        {
            var config = AppConfiguration.Instance.GetSqlConnection(connectionName);
            using var connection = await UniversalSqlDbFactory.CreateAsync(config.Driver, config.ConnectionString);
            connection.Should().NotBeNull();
            connection.Should().BeOfType(expectedType);
            connection.ConnectionType.Should().Match(v => config.Driver.Equals(v, StringComparison.OrdinalIgnoreCase));
        }

        public static IEnumerable<object[]> Connections(string exclude)
        {
            return SqlConnectionSources.SqlConnections(exclude).Select(info =>
                new object[]
                {
                    info.Name,
                    info.Driver switch
                    {
                        "mssql" => typeof(MssqlDbConnection),
                        "mysql" => typeof(MysqlDbConnection),
                        "npgsql" => typeof(PostgresDbConnection),
                        "sqlite" => typeof(SqliteDbConnection),
                        "oracle" => typeof(OracleDbConnection),
                        _ => throw new ArgumentException($"Unknown driver {info.Driver}")
                    }
                });
        }
    }
}
