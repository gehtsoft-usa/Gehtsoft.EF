using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb
{
    public static class UniversalSqlDbFactory
    {
        public const string MSSQL = "mssql";
        internal const string MSSQL_ASSEMBLY = "Gehtsoft.EF.Db.MssqlDb";
        internal const string MSSQL_CLASS = "Gehtsoft.EF.Db.MssqlDb.MssqlDbConnectionFactory";

        public const string MYSQL = "mysql";
        internal const string MYSQL_ASSEMBLY = "Gehtsoft.EF.Db.MysqlDb";
        internal const string MYSQL_CLASS = "Gehtsoft.EF.Db.MysqlDb.MysqlDbConnectionFactory";

        public const string ORACLE = "oracle";
        internal const string ORACLE_ASSEMBLY = "Gehtsoft.EF.Db.OracleDb";
        internal const string ORACLE_CLASS = "Gehtsoft.EF.Db.OracleDb.OracleDbConnectionFactory";

        public const string POSTGRES = "npgsql";
        internal const string POSTGRES_ASSEMBLY = "Gehtsoft.EF.Db.PostgresDb";
        internal const string POSTGRES_CLASS = "Gehtsoft.EF.Db.PostgresDb.PostgresDbConnectionFactory";

        public const string SQLITE = "sqlite";
        internal const string SQLITE_ASSEMBLY = "Gehtsoft.EF.Db.SqliteDb";
        internal const string SQLITE_CLASS = "Gehtsoft.EF.Db.SqliteDb.SqliteDbConnectionFactory";

        public static string[] SupportedDatabases
        {
            get
            {
                return new string[] { MSSQL, MYSQL, POSTGRES, SQLITE, ORACLE };
            }
        }

        public static bool FindDriver(string dbname, out string assemblyName, out string className)
        {
            if (dbname == MSSQL)
            {
                assemblyName = MSSQL_ASSEMBLY;
                className = MSSQL_CLASS;
                return true;
            }

            if (dbname == MYSQL)
            {
                assemblyName = MYSQL_ASSEMBLY;
                className = MYSQL_CLASS;
                return true;
            }

            if (dbname == ORACLE)
            {
                assemblyName = ORACLE_ASSEMBLY;
                className = ORACLE_CLASS;
                return true;
            }

            if (dbname == POSTGRES)
            {
                assemblyName = POSTGRES_ASSEMBLY;
                className = POSTGRES_CLASS;
                return true;
            }

            if (dbname == SQLITE)
            {
                assemblyName = SQLITE_ASSEMBLY;
                className = SQLITE_CLASS;
                return true;
            }

            assemblyName = className = null;
            return false;
        }

        public static SqlDbConnectionFactory LoadFactory(string dbname)
        {
            if (!FindDriver(dbname, out string assemblyName, out string className))
                return null;

            Assembly assembly = Assembly.Load(assemblyName);

            if (assembly == null)
                throw new ArgumentException($"The assembly {assemblyName} that consists of the factory for the requested database {dbname} is not found or cannot be loaded");

            foreach (Type type in assembly.GetTypes())
            {
                if (type.FullName == className)
                {
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        var parameters = method.GetParameters();

                        if (method.Name == "Create" &&
                            typeof(SqlDbConnection).IsAssignableFrom(method.ReturnType) &&
                            parameters?.Length == 1 &&
                            parameters[0].ParameterType == typeof(string))
                        {
                            return (SqlDbConnectionFactory)method.CreateDelegate(typeof(SqlDbConnectionFactory), null);
                        }
                    }
                }
            }
            throw new ArgumentException($"The assembly {assemblyName} does not consists of class {className} that contains a static method with the expected signature");
        }
        public static SqlDbConnectionFactoryAsync LoadAsyncFactory(string dbname)
        {
            if (!FindDriver(dbname, out string assemblyName, out string className))
                return null;

            Assembly assembly = Assembly.Load(assemblyName);

            if (assembly == null)
                throw new ArgumentException($"The assembly {assemblyName} that consists of the factory for the requested database {dbname} is not found or cannot be loaded");

            foreach (Type type in assembly.GetTypes())
            {
                if (type.FullName == className)
                {
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        var parameters = method.GetParameters();

                        if (method.Name == "CreateAsync" &&
                            typeof(Task<SqlDbConnection>).IsAssignableFrom(method.ReturnType) &&
                            parameters?.Length == 2 &&
                            parameters[0].ParameterType == typeof(string) &&
                            parameters[1].ParameterType == typeof(CancellationToken?))
                        {
                            return (SqlDbConnectionFactoryAsync)method.CreateDelegate(typeof(SqlDbConnectionFactoryAsync), null);
                        }
                    }
                }
            }
            throw new ArgumentException($"The assembly {assemblyName} does not consists of class {className} that contains a static method with the expected signature");
        }

        public static SqlDbConnection Create(string dbname, string connectionString)
        {
            IResiliencyPolicy resiliencyPolicy = ResiliencyPolicyDictionary.Instance.GetPolicy(connectionString);
            if (resiliencyPolicy == null)
                return LoadFactory(dbname)?.Invoke(connectionString);
            else
                return resiliencyPolicy.Execute(() => LoadFactory(dbname)?.Invoke(connectionString));
        }

        public static async Task<SqlDbConnection> CreateAsync(string dbname, string connectionString)
        {
            IResiliencyPolicy resiliencyPolicy = ResiliencyPolicyDictionary.Instance.GetPolicy(connectionString);
            var factory = LoadAsyncFactory(dbname);
            if (factory == null)
                throw new ArgumentException($"The database {dbname} is not found", nameof(dbname));
            if (resiliencyPolicy == null)
                return await factory.Invoke(connectionString, null);
            else
                return await resiliencyPolicy.ExecuteAsync(token1 => factory.Invoke(connectionString, token1), CancellationToken.None);
        }

        public static async Task<SqlDbConnection> CreateAsync(string dbname, string connectionString, CancellationToken token)
        {
            IResiliencyPolicy resiliencyPolicy = ResiliencyPolicyDictionary.Instance.GetPolicy(connectionString);

            var factory = LoadAsyncFactory(dbname);
            if (factory == null)
                throw new ArgumentException($"The database {dbname} is not found", nameof(dbname));

            if (resiliencyPolicy == null)
                return await factory.Invoke(connectionString, token);
            else
                return await resiliencyPolicy.ExecuteAsync(token1 => factory.Invoke(connectionString, token1), token);
        }
    }
}
