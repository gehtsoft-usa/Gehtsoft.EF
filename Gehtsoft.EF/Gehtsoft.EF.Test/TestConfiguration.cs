using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Gehtsoft.EF.Test
{
    public class TestConfiguration
    {
        public IConfiguration Config { get; }

        private TestConfiguration()
        {
            Config = new ConfigurationBuilder()
                .AddJsonFile("Configuration.json", true)
                .Build();
        }

        private static TestConfiguration gInstance = null;

        public static TestConfiguration Instance => gInstance ?? new TestConfiguration();

        public string this[string key] => Config[key];

        public string Get(string key, string defaultValue = null) => Config[key] ?? defaultValue;

        public T Get<T>(string key, T defaultValue = default)
            => Config.GetValue<T>(key, defaultValue);
    }
}
