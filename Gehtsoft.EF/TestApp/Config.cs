using FluentAssertions;
using Gehtsoft.Tools.ConfigurationProfile;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp
{
    class Config
    {
        private readonly ProfileFactory mProfileFactory;
        private static Config gConfig;
        public static Config Instance = gConfig ?? (gConfig = new Config());

        private Config()
        {

            mProfileFactory = new ProfileFactory();
            string location = Path.GetFullPath(typeof(Config).Assembly.Location);
            FileInfo fi = new FileInfo(location);
            string configFile = Path.Combine(fi.DirectoryName, "config.ini");
            File.Exists(configFile).Should().BeTrue("The config.ini file must exists and must contains proper configuration for local test database. Refer to config.ini.template as an example.");
            mProfileFactory.Configure(configFile, false, false);
        }

        public string OracleTns => mProfileFactory.Profile.Get<string>("connectionStrings", "oracle_tns");
        public string OracleUser => mProfileFactory.Profile.Get<string>("connectionStrings", "oracle_user");
        public string OraclePassword => mProfileFactory.Profile.Get<string>("connectionStrings", "oracle_password");
        public string Mssql => mProfileFactory.Profile.Get<string>("connectionStrings", "mssql");
        public string Mysql => mProfileFactory.Profile.Get<string>("connectionStrings", "mysql");
        public string Postgres => mProfileFactory.Profile.Get<string>("connectionStrings", "postgres");
        public string Mongo => mProfileFactory.Profile.Get<string>("connectionStrings", "mongo");

    }
}
