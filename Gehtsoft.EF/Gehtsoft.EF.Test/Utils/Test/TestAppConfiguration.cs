using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FluentAssertions;
using Gehtsoft.EF.Test.Utils;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Gehtsoft.EF.TestInfrastructure.Test.Utils
{
    public class TestAppConfiguration
    {
        public class AppConfiguration1 : AppConfiguration
        {
            public AppConfiguration1(string json)
            {
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
                Config = new ConfigurationBuilder()
                               .AddJsonStream(ms)
                               .Build();
            }
        }

        [Fact]
        public void ReadValue()
        {
            const string json = "{ \"sqlConnections\" : { \"connection1\" : { \"driver\" : \"driver1\", \"connectionString\" : \"connection-string\", \"enabled\" : \"true\" } } }";
            AppConfiguration1 config = new AppConfiguration1(json);

            config.Get<bool>("sqlConnections:connection1:enabled").Should().BeTrue();
            config.Get("sqlConnections:connection1:driver").Should().Be("driver1");
        }

        [Fact]
        public void ReadSqlConfig()
        {
            const string json = "{ \"sqlConnections\" : { \"connection1\" : { \"driver\" : \"driver1\", \"connectionString\" : \"connection-string\", \"enabled\" : \"true\" } } }";
            AppConfiguration1 config = new AppConfiguration1(json);
            var info = config.GetSqlConnection("connection1");
            info.Should().NotBeNull();
            info.Driver.Should().Be("driver1");
            info.ConnectionString.Should().Be("connection-string");
            info.Enabled.Should().BeTrue();
        }

        [Fact]
        public void ReadMongoConfig()
        {
            const string json = "{ \"nosqlConnections\" : { \"mongo1\" : { \"driver\" : \"mongo\", \"connectionString\" : \"connection-string\", \"enabled\" : \"true\" } } }";
            AppConfiguration1 config = new AppConfiguration1(json);
            var info = config.GetMongoConnection();
            info.Should().NotBeNull();
            info.Driver.Should().Be("mongo");
            info.ConnectionString.Should().Be("connection-string");
            info.Enabled.Should().BeTrue();
        }

        [Fact]
        public void ReadSqlConnections()
        {
            const string json = "{ \"sqlConnections\" : { \"connection1\" : { \"driver\" : \"driver1\", \"connectionString\" : \"connection-string\", \"enabled\" : \"true\" }, \"connection2\" : { \"driver\" : \"driver2\", \"connectionString\" : \"connection-string2\", \"enabled\" : \"false\" } } }";
            AppConfiguration1 config = new AppConfiguration1(json);
            var infos = config.GetSqlConnections().ToArray();

            infos.Should().HaveCount(2);
            infos[0].Name.Should().Be("connection1");
            infos[1].Name.Should().Be("connection2");
        }

        [Fact]
        public void TestConnectionSource()
        {
            const string json = "{ \"sqlConnections\" : { \"connection1\" : { \"driver\" : \"driver1\", \"connectionString\" : \"connection-string\", \"enabled\" : \"true\" }, \"connection2\" : { \"driver\" : \"driver2\", \"connectionString\" : \"connection-string2\", \"enabled\" : \"true\" } } }";
            var config = new AppConfiguration1(json);
            var infos = SqlConnectionSources.SqlConnections(config).ToArray();

            infos.Should().HaveCount(2);
            infos[0].Name.Should().Be("connection1");
            infos[1].Name.Should().Be("connection2");
        }

        [Fact]
        public void TestConnectionSource_Exlcude_ByName()
        {
            const string json = "{ \"sqlConnections\" : { \"connection1\" : { \"driver\" : \"driver1\", \"connectionString\" : \"connection-string\", \"enabled\" : \"true\" }, \"connection2\" : { \"driver\" : \"driver2\", \"connectionString\" : \"connection-string2\", \"enabled\" : \"true\" } } }";
            var config = new AppConfiguration1(json);
            var infos = SqlConnectionSources.SqlConnections(config, "-connection1").ToArray();

            infos.Should().HaveCount(1);
            infos[0].Name.Should().Be("connection2");
        }

        [Fact]
        public void TestConnectionSource_Exlcude_ByDriver()
        {
            const string json = "{ \"sqlConnections\" : { \"connection1\" : { \"driver\" : \"driver1\", \"connectionString\" : \"connection-string\", \"enabled\" : \"true\" }, \"connection2\" : { \"driver\" : \"driver2\", \"connectionString\" : \"connection-string2\", \"enabled\" : \"false\" } } }";
            var config = new AppConfiguration1(json);
            var infos = SqlConnectionSources.SqlConnections(config, "-driver2").ToArray();

            infos.Should().HaveCount(1);
            infos[0].Name.Should().Be("connection1");
        }
    }
}
