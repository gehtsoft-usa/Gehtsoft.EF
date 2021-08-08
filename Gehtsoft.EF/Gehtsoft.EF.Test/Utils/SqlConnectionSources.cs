using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Test.Utils
{
    class SqlConnectionSources
    {
        public static IEnumerable<AppConfiguration.ConnectionInfo> Connections(string exclude = null)
                => Connections(AppConfiguration.Instance, exclude);

        public static IEnumerable<AppConfiguration.ConnectionInfo> Connections(AppConfiguration config, string exclude = null)
        {
            string[] _exclude;
            
            if (!string.IsNullOrEmpty(exclude))
                _exclude = exclude.Split(',', StringSplitOptions.RemoveEmptyEntries);
            else
                _exclude = new string[] { };

            string[] _includeOnly = null;

            var globalFilter = config.Get("global-filter:sql-drivers", "all");
            if (globalFilter != "all")
                _includeOnly = globalFilter.Split(",", StringSplitOptions.RemoveEmptyEntries);


            for (int i = 0; i < _exclude.Length; i++)
            {
                if (_exclude[i].StartsWith("+"))
                    _exclude[i] = "";
                else if (_exclude[i].StartsWith("-"))
                    _exclude[i] = _exclude[i].Substring(1);
            }

            foreach (var connection in config.GetSqlConnections())
            {
                if (_exclude.Any(s => s == connection.Name || s == connection.Driver) || !connection.Enabled)
                    continue;

                if (_includeOnly != null && _includeOnly.Length > 0)
                    if (!_includeOnly.Any(s => s == connection.Name || s == connection.Driver))
                        continue;

                yield return connection;
            }
        }

        public static IEnumerable<object[]> ConnectionNames(string exclude = null)
            => Connections(exclude).Select(c => new object[] { c.Name });
    }
}
