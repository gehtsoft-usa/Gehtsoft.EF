using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Gehtsoft.EF.Test.Utils
{
    public class AppConfiguration
    {
        public IConfiguration Config { get; protected set; }

        protected AppConfiguration()
        {
            Config = new ConfigurationBuilder()
                .AddJsonFile("Configuration.json", true)
                .Build();
        }

        private readonly static AppConfiguration gInstance = null;

        public static AppConfiguration Instance => gInstance ?? new AppConfiguration();

        public string this[string key] => Config[key];

        public string Get(string key, string defaultValue = null) => Config[key] ?? defaultValue;

        public T Get<T>(string key, T defaultValue = default)
            => Config.GetValue<T>(key, defaultValue);

        public class ConnectionInfo
        {
            public string Name { get; set; }
            public string Driver { get; set; }
            public bool Enabled { get; set; }
            public string ConnectionString { get; set; }
        }

        public const string SQLCONNECTIONS = "sqlConnections";
        public const string NOSQLCONNECTIONS = "nosqlConnections";

        private IEnumerable<ConnectionInfo> GetConnections(string sectionName)
        {
            var section = Config.GetSection(sectionName);
            foreach (var child in section.GetChildren())
            {
                var info = GetConnection(sectionName, child.Key);
                if (info != null)
                    yield return info;
            }
        }

        public IEnumerable<ConnectionInfo> GetSqlConnections() => GetConnections(SQLCONNECTIONS);

        public IEnumerable<ConnectionInfo> GetNoSqlConnections() => GetConnections(NOSQLCONNECTIONS);
        public ConnectionInfo GetSqlConnection(string connectionName) => GetConnection(SQLCONNECTIONS, connectionName);
        public ConnectionInfo GetNoSqlConnection(string connectionName) => GetConnection(NOSQLCONNECTIONS, connectionName);

        public ConnectionInfo GetMongoConnection() => GetNoSqlConnections().FirstOrDefault(c => c.Driver == "mongo");

        public ConnectionInfo GetConnection(string type, string connectionName)
        {
            var section = Config.GetSection($"{type}:{connectionName}");
            if (section == null)
                return null;

            ConnectionInfo info = new ConnectionInfo()
            {
                Name = connectionName,
                Driver = section["driver"],
                ConnectionString = section["connectionString"],
                Enabled = section["enabled"] == "true",
            };

            if (!string.IsNullOrEmpty(info.Driver) && !string.IsNullOrEmpty(info.ConnectionString))
                return info;

            return null;
        }
    }
}
