using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Test.Utils
{
    internal static class SqlConnectionSources
    {
        public static IEnumerable<AppConfiguration.ConnectionInfo> Connections(string exclude = null)
                => Connections(AppConfiguration.Instance, exclude);

        public static IEnumerable<AppConfiguration.ConnectionInfo> Connections(AppConfiguration config, string exclude = null)
        {
            HashSet<string> _exclude = new HashSet<string>();
            HashSet<string> _includeOnly = new HashSet<string>();

            void processFilter(string filter)
            {
                var t = filter.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var s in t)
                {
                    if (s.Length > 0 && s[0] == '-')
                    {
                        var s1 = s.Substring(1);
                        if (!_exclude.Contains(s1))
                            _exclude.Add(s1);
                    }
                    else if (s.Length > 0 && s[0] == '+')
                    {
                        var s1 = s.Substring(1);
                        if (!_includeOnly.Contains(s1))
                            _includeOnly.Add(s1);
                    }
                }
            }

            if (!string.IsNullOrEmpty(exclude))
                processFilter(exclude);
          

            var globalFilter = config.Get("global-filter:sql-drivers", "all");
            if (globalFilter != "all" && !string.IsNullOrEmpty(globalFilter))
                processFilter(globalFilter);

            foreach (var connection in config.GetSqlConnections())
            {
                if (_exclude.Contains(connection.Name) || _exclude.Contains(connection.Driver) || (!connection.Enabled && !_includeOnly.Contains(connection.Name)))
                    continue;

                if (_includeOnly.Count > 0)
                    if (!_includeOnly.Contains(connection.Name) && !_includeOnly.Contains(connection.Driver))
                        continue;

                yield return connection;
            }
        }

        /// <summary>
        /// Gets list of the connections from the config
        /// </summary>
        /// <param name="flags">Comma-separated list of the connection or driver names with `+` prefix to include only and `-` prefix to exclude</param>
        /// <returns></returns>
        public static IEnumerable<object[]> ConnectionNames(string flags = null)
            => Connections(flags).Select(c => new object[] { c.Name });
    }
}
