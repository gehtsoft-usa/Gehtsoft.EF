using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;

namespace Gehtsoft.EF.Test.Utils
{
    internal static class SqlConnectionSources
    {
        public static IEnumerable<AppConfiguration.ConnectionInfo> SqlConnections(string filter = null)
                => SqlConnections(AppConfiguration.Instance, filter);

        public static IEnumerable<AppConfiguration.ConnectionInfo> SqlConnections(AppConfiguration config, string filter = null)
            => Connections(config, filter, "global-filter:sql-drivers", () => config.GetSqlConnections());

        public static IEnumerable<AppConfiguration.ConnectionInfo> MongoConnections()
                => MongoConnections(AppConfiguration.Instance);

        public static IEnumerable<AppConfiguration.ConnectionInfo> MongoConnections(AppConfiguration config)
            => Connections(config, "+mongo", "global-filter:non-existent", () => config.GetNoSqlConnections());

        private static IEnumerable<AppConfiguration.ConnectionInfo> Connections(AppConfiguration config,
            string exclude,
            string globalConfigName, Func<IEnumerable<AppConfiguration.ConnectionInfo>> connectionsGet)
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


            var globalFilter = config.Get(globalConfigName, "all");
            if (globalFilter != "all" && !string.IsNullOrEmpty(globalFilter))
                processFilter(globalFilter);

            foreach (var connection in connectionsGet())
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
        /// <param name="flags1">more flags</param>
        /// <returns></returns>
        public static IEnumerable<object[]> SqlConnectionNames(string flags, params string[] flags1)
        {
            StringBuilder t = new StringBuilder();
            if (!string.IsNullOrEmpty(flags))
                t.Append(flags);

            foreach (var flag in flags1)
            {
                if (!string.IsNullOrEmpty(flag))
                {
                    if (t.Length > 0)
                        t.Append(",");
                    t.Append(flag);
                }
            }

            return SqlConnectionNames(t.ToString());
        }

        /// <summary>
        /// Gets list of the connections from the config
        /// </summary>
        /// <param name="flags">Comma-separated list of the connection or driver names with `+` prefix to include only and `-` prefix to exclude</param>
        /// <returns></returns>
        public static IEnumerable<object[]> SqlConnectionNames(string flags = null)
            => SqlConnections(flags).Select(c => new object[] { c.Name });

        /// <summary>
        /// Gets list of the connections from the config
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<object[]> MongoConnectionNames()
            => MongoConnections().Select(c => new object[] { c.Name });

        /// <summary>
        /// Gets list of the connections from the config
        /// </summary>
        /// <param name="flags">Comma-separated list of the connection or driver names with `+` prefix to include only and `-` prefix to exclude</param>
        /// <returns></returns>
        public static IEnumerable<object[]> SqlConnectionNamesWithArgs(string flags, Type argsSourceType, string argsSourceName)
            => ConnectionNamesWithArgs(() => SqlConnections(flags), argsSourceType, argsSourceName);

        /// <summary>
        /// Gets list of the connections from the config
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<object[]> MongoConnectionNamesWithArgs(Type argsSourceType, string argsSourceName)
            => ConnectionNamesWithArgs(() => MongoConnections(), argsSourceType, argsSourceName);


        private static IEnumerable<object[]> ConnectionNamesWithArgs(Func<IEnumerable<AppConfiguration.ConnectionInfo>> connections, Type argsSourceType, string argsSourceName)
        {
            var m = argsSourceType.GetMethod(argsSourceName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (m == null)
                throw new ArgumentException($"Type {argsSourceType.Name} doesn't have method {argsSourceName}");
            if (!m.IsStatic || m.GetParameters()?.Length != 1 || m.GetParameters()[0].ParameterType != typeof(string) || m.ReturnType != typeof(IEnumerable<object[]>))
                throw new ArgumentException($"Method {argsSourceType.Name}.{argsSourceName} is not a static method than take a string (connection name) that returns IEnumerable<object[]>");
            return ConnectionNamesWithArgsIterator(connections, (s) => m.Invoke(null, new string[] { s }) as IEnumerable<object[]>);

        }

        private static IEnumerable<object[]> ConnectionNamesWithArgsIterator(Func<IEnumerable<AppConfiguration.ConnectionInfo>> getConnections, Func<string, IEnumerable<object[]>> getParameterSets)
        {
            foreach (var connection in getConnections())
            {
                var parameterSets = getParameterSets(connection.Driver);

                foreach (var args in parameterSets)
                {
                    var r = new object[args.Length + 1];
                    r[0] = connection.Name;
                    for (int i = 0; i < args.Length; i++)
                        r[i + 1] = args[i];
                    yield return r;
                }
            }
        }
    }
}
