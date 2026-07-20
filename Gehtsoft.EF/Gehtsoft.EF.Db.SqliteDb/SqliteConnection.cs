using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using System.Threading.Tasks;
using System.Threading;
using System.Data;
using System;
using System.Globalization;
using System.Runtime.InteropServices;

#pragma warning disable S6966 // false positive: methods use sync/async branching pattern

namespace Gehtsoft.EF.Db.SqliteDb
{
    public class SqliteDbConnection : SqlDbConnection
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        protected SqliteConnection mSqlConnection;
        private SqliteDbTransaction mCurrentTransaction;

        public override string ConnectionType => "sqlite";

        public SqliteDbConnection(SqliteConnection connection) : base(connection)
        {
            mSqlConnection = connection;
            SetupFunctions(connection);
            if (SqliteGlobalOptions.EnableSpatial)
                EnableSpatialite(connection);
        }

        private static int mSqliteSymbolsPromoted;
        private static int mSpatialitePreloaded;

        [System.Runtime.InteropServices.DllImport("libdl.so.2", EntryPoint = "dlopen")]
        private static extern IntPtr LinuxDlopen(string fileName, int flags);

        [System.Runtime.InteropServices.DllImport("libSystem.dylib", EntryPoint = "dlopen")]
        private static extern IntPtr MacDlopen(string fileName, int flags);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr WindowsLoadLibraryEx(string fileName, IntPtr file, uint flags);

        // On Unix, mod_spatialite is compiled to reference the sqlite3_* symbols of the hosting engine
        // directly (not through the extension API table). SQLitePCLRaw loads e_sqlite3 with local symbol
        // visibility, so those symbols are invisible to the extension and calling it segfaults. Promote
        // the already-loaded e_sqlite3 to the global symbol scope (dlopen RTLD_GLOBAL) so the extension
        // binds against it. Windows has NO such problem — its mod_spatialite.dll imports no sqlite3 DLL
        // and binds through the extension API routines table; see PreloadSpatialiteWindows for the real
        // Windows blocker (dependency-DLL discovery).
        private static void PromoteSqliteSymbolsForSpatialite()
        {
            if (System.Threading.Interlocked.Exchange(ref mSqliteSymbolsPromoted, 1) != 0)
                return;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;
            string path = LocateNativeSqlite();
            if (path == null)
                return;
            const int RTLD_NOW = 0x2;
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    MacDlopen(path, RTLD_NOW | 0x8);     // macOS RTLD_GLOBAL = 0x8
                else
                    LinuxDlopen(path, RTLD_NOW | 0x100); // Linux RTLD_GLOBAL = 0x100
            }
            catch
            {
                // best effort; LoadExtension will surface a clear error if the symbols are still missing
            }
        }

        private static string LocateNativeSqlite()
        {
            bool osx = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            string file = osx ? "libe_sqlite3.dylib" : "libe_sqlite3.so";
            string os = osx ? "osx" : "linux";
            string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
            string baseDir = AppContext.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, file),
                Path.Combine(baseDir, "runtimes", os + "-" + arch, "native", file),
            };
            for (int i = 0; i < candidates.Length; i++)
                if (File.Exists(candidates[i]))
                    return candidates[i];
            return null;
        }

        // Windows: mod_spatialite.dll itself imports no sqlite3 symbols, so there is no symbol-scope
        // problem. What fails is dependency resolution: mod_spatialite depends on libgeos_c, libproj,
        // librttopo, … which in turn chain to libgeos, libstdc++, libcurl, libsqlite3-0, …, and every
        // one of those DLLs sits in the same native folder — a folder that is on neither the app base
        // dir nor PATH. A plain LoadLibraryW("mod_spatialite") therefore fails with error 126 when the
        // loader cannot find those dependencies. Pre-loading the DLL by its absolute path with
        // LOAD_WITH_ALTERED_SEARCH_PATH makes the loader resolve the whole dependency graph from the
        // module's own directory. The subsequent LoadExtension(fullPath) reuses the already-loaded
        // module and only runs the extension entry point. Returns the located full path (to hand to
        // LoadExtension) or null when the library could not be found (fall back to the configured name).
        private static string PreloadSpatialiteWindows()
        {
            string located = LocateSpatialiteWindows();
            if (located == null)
                return null;
            if (System.Threading.Interlocked.Exchange(ref mSpatialitePreloaded, 1) == 0)
            {
                const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x8;
                try
                {
                    WindowsLoadLibraryEx(located, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
                }
                catch
                {
                    // best effort; LoadExtension below surfaces a clear error if the load still fails
                }
            }
            return located;
        }

        private static string LocateSpatialiteWindows()
        {
            string configured = SqliteGlobalOptions.SpatialiteLibrary;

            // An explicit path (or a name that resolves to a file next to the app) wins as-is.
            if (!string.IsNullOrEmpty(configured))
            {
                if (File.Exists(configured))
                    return Path.GetFullPath(configured);
                string withExt = configured.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? configured : configured + ".dll";
                if (File.Exists(withExt))
                    return Path.GetFullPath(withExt);
            }

            string arch;
            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.X86:
                    arch = "x86";
                    break;
                case Architecture.Arm64:
                    arch = "arm64";
                    break;
                default:
                    arch = "x64";
                    break;
            }
            string baseDir = AppContext.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "mod_spatialite.dll"),
                Path.Combine(baseDir, "runtimes", "win-" + arch, "native", "mod_spatialite.dll"),
            };
            for (int i = 0; i < candidates.Length; i++)
                if (File.Exists(candidates[i]))
                    return candidates[i];
            return null;
        }

        private static void EnableSpatialite(SqliteConnection connection)
        {
            string extension = SqliteGlobalOptions.SpatialiteLibrary;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string located = PreloadSpatialiteWindows();
                if (located != null)
                    extension = located;
            }
            else
            {
                PromoteSqliteSymbolsForSpatialite();
            }

            connection.EnableExtensions(true);
            connection.LoadExtension(extension);

            // Bootstrap the spatial metadata once per database (guarded so it is idempotent across
            // connections to the same file, and re-created for each in-memory database).
            using (var check = connection.CreateCommand())
            {
                check.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'spatial_ref_sys'";
                long present = Convert.ToInt64(check.ExecuteScalar(), CultureInfo.InvariantCulture);
                if (present == 0)
                {
                    using (var init = connection.CreateCommand())
                    {
                        init.CommandText = "SELECT InitSpatialMetaData(1)";
                        init.ExecuteNonQuery();
                    }
                }
            }
        }

        private static void SetupFunctions(SqliteConnection connection)
        {
            if (SqliteGlobalOptions.StoreDateAsString)
            {
                connection.CreateFunction("YEAR", (string s) =>
                {
                    if (string.IsNullOrEmpty(s))
                        return 0;
                    if (DateTime.TryParseExact(s, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d))
                    {
                        d = d.ToUniversalTime();
                        return d.Year;
                    }
                    return 0;
                });

                connection.CreateFunction("MONTH", (string s) =>
                {
                    if (string.IsNullOrEmpty(s))
                        return 0;
                    if (DateTime.TryParseExact(s, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d))
                    {
                        d = d.ToUniversalTime();
                        return d.Month;
                    }
                    return 0;
                });

                connection.CreateFunction("DAY", (string s) =>
                {
                    if (string.IsNullOrEmpty(s))
                        return 0;
                    if (DateTime.TryParseExact(s, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d))
                    {
                        d = d.ToUniversalTime();
                        return d.Day;
                    }
                    return 0;
                });

                connection.CreateFunction("HOUR", (string s) =>
                {
                    if (string.IsNullOrEmpty(s))
                        return 0;
                    if (DateTime.TryParseExact(s, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d))
                    {
                        d = d.ToUniversalTime();
                        return d.Hour;
                    }
                    return 0;
                });

                connection.CreateFunction("MINUTE", (string s) =>
                {
                    if (string.IsNullOrEmpty(s))
                        return 0;
                    if (DateTime.TryParseExact(s, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d))
                    {
                        d = d.ToUniversalTime();
                        return d.Minute;
                    }
                    return 0;
                });

                connection.CreateFunction("SECOND", (string s) =>
                {
                    if (string.IsNullOrEmpty(s))
                        return 0;
                    if (DateTime.TryParseExact(s, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d))
                    {
                        d = d.ToUniversalTime();
                        return d.Second;
                    }
                    return 0;
                });
            }
            else
            {
                connection.CreateFunction("YEAR", (double? d) =>
                {
                    if (d == null)
                        return (double?)null;
                    return DateTime.FromOADate(d.Value).Year;
                });

                connection.CreateFunction("MONTH", (double? d) =>
                {
                    if (d == null)
                        return (double?)null;
                    return DateTime.FromOADate(d.Value).Month;
                });

                connection.CreateFunction("DAY", (double? d) =>
                {
                    if (d == null)
                        return (double?)null;
                    return DateTime.FromOADate(d.Value).Day;
                });

                connection.CreateFunction("HOUR", (double? d) =>
                {
                    if (d == null)
                        return (double?)null;
                    return DateTime.FromOADate(d.Value).Hour;
                });

                connection.CreateFunction("MINUTE", (double? d) =>
                {
                    if (d == null)
                        return (double?)null;
                    return DateTime.FromOADate(d.Value).Minute;
                });

                connection.CreateFunction("SECOND", (double? d) =>
                {
                    if (d == null)
                        return (double?)null;
                    return DateTime.FromOADate(d.Value).Second;
                });
            }

            connection.CreateFunction("SLEFT", (string s, int l) =>
            {
                if (s == null)
                    return null;
                if (l > s.Length)
                    l = s.Length;
                if (l == s.Length)
                    return s;
                return s.Substring(0, l);
            });

            connection.CreateFunction("TOSTRING", (object v) =>
            {
                if (v == null)
                    return null;
                if (v is int i)
                    return i.ToString(CultureInfo.InvariantCulture);
                if (v is double d)
                    return d.ToString(CultureInfo.InvariantCulture);
                if (v is float f)
                    return f.ToString(CultureInfo.InvariantCulture);
                if (v is decimal dc)
                    return dc.ToString(CultureInfo.InvariantCulture);
                if (v is string s)
                    return s;
                return v.ToString();
            });

            connection.CreateFunction("TOREAL", (object v) =>
            {
                if (v == null)
                    return 0;
                if (v is int i)
                    return (double)i;
                if (v is double d)
                    return d;
                if (v is float f)
                    return (double)f;
                if (v is decimal dc)
                    return (double)dc;
                if (v is long l)
                    return (double)l;
                if (v is string s)
                    return double.TryParse(s, out double dv) ? dv : 0;
                return 0;
            });
        }
        protected override SqlDbQuery ConstructQuery()
        {
            return new SqliteDbQuery(this, mSqlConnection.CreateCommand(), gSpecifics);
        }

        protected override SqlDbQuery ConstructQuery(string queryText)
        {
            return new SqliteDbQuery(this, mSqlConnection.CreateCommand(), gSpecifics) { CommandText = queryText };
        }

        public override SqlDbLanguageSpecifics GetLanguageSpecifics()
        {
            return gSpecifics;
        }

        private static readonly SqliteDbLanguageSpecifics gSpecifics = new SqliteDbLanguageSpecifics();

        public override CreateTableBuilder GetCreateTableBuilder(TableDescriptor descriptor)
        {
            return new SqliteCreateTableBuilder(gSpecifics, descriptor);
        }

        public override InsertQueryBuilder GetInsertQueryBuilder(TableDescriptor descriptor, bool ignoreAutoIncrement = false)
        {
            return new SqliteInsertQueryBuilder(gSpecifics, descriptor, ignoreAutoIncrement);
        }

        public override InsertSelectQueryBuilder GetInsertSelectQueryBuilder(TableDescriptor descriptor, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement = false)
        {
            return new SqliteInsertSelectQueryBuilder(gSpecifics, descriptor, selectQuery, ignoreAutoIncrement);
        }

        public override HierarchicalSelectQueryBuilder GetHierarchicalSelectQueryBuilder(TableDescriptor descriptor, TableDescriptor.ColumnInfo parentReferenceColumn, string rootParameter = null)
        {
            return new SqliteHierarchicalSelectQueryBuilder(gSpecifics, descriptor, parentReferenceColumn, rootParameter);
        }

        protected override async Task<TableDescriptor[]> SchemaCore(bool sync, CancellationToken? token)
        {
            List<TableDescriptor> tables = new List<TableDescriptor>();

            using (SqlDbQuery query = GetQuery("select NAME from SQLITE_MASTER where TYPE=@type"))
            {
                query.BindParam("type", "table");
                if (sync)
                {
                    query.ExecuteReader();
                    while (query.ReadNext())
                        tables.Add(new TableDescriptor(query.GetValue<string>(0)));
                }
                else
                {
                    await query.ExecuteReaderAsync(token);
                    while (await query.ReadNextAsync(token))
                        tables.Add(new TableDescriptor(query.GetValue<string>(0)));
                }
            }

            using (SqlDbQuery query = GetQuery("select NAME from SQLITE_MASTER where TYPE=@type"))
            {
                query.BindParam("type", "view");
                if (sync)
                {
                    query.ExecuteReader();
                    while (query.ReadNext())
                        tables.Add(new TableDescriptor(query.GetValue<string>(0)) { View = true });
                }
                else
                {
                    await query.ExecuteReaderAsync(token);
                    while (await query.ReadNextAsync(token))
                        tables.Add(new TableDescriptor(query.GetValue<string>(0)) { View = true });
                }
            }

            foreach (TableDescriptor descriptor in tables)
            {
                using (SqlDbQuery query = GetQuery($"pragma table_info({descriptor.Name})"))
                {
                    if (sync)
                    {
                        query.ExecuteReader();
                        while (query.ReadNext())
                        {
                            descriptor.Add(new TableDescriptor.ColumnInfo() { Name = query.GetValue<string>("name") });
                        }
                    }
                    else
                    {
                        await query.ExecuteReaderAsync(token);
                        while (await query.ReadNextAsync(token))
                        {
                            descriptor.Add(new TableDescriptor.ColumnInfo() { Name = query.GetValue<string>("name") });
                        }
                    }
                }
            }

            return tables.ToArray();
        }

        protected override async Task<TableIndexInfo[]> GetTableIndexesCore(string tableName, bool sync, CancellationToken? token)
        {
            var indexes = new List<(string Name, bool Unique, bool Primary)>();

            using (SqlDbQuery query = GetQuery($"PRAGMA index_list('{tableName}')", true))
            {
                if (sync)
                {
                    query.ExecuteReader();
                    while (query.ReadNext())
                        indexes.Add(ReadIndexListRow(query));
                }
                else
                {
                    await query.ExecuteReaderAsync(token);
                    while (await query.ReadNextAsync(token))
                        indexes.Add(ReadIndexListRow(query));
                }
            }

            var rows = new List<RawIndexColumn>();
            for (int i = 0; i < indexes.Count; i++)
            {
                var idx = indexes[i];
                bool any = false;
                using (SqlDbQuery query = GetQuery($"PRAGMA index_info('{idx.Name}')", true))
                {
                    if (sync)
                    {
                        query.ExecuteReader();
                        while (query.ReadNext())
                            any |= AddIndexInfoRow(rows, query, idx);
                    }
                    else
                    {
                        await query.ExecuteReaderAsync(token);
                        while (await query.ReadNextAsync(token))
                            any |= AddIndexInfoRow(rows, query, idx);
                    }
                }
                if (!any)
                    rows.Add(new RawIndexColumn { IndexName = idx.Name, Column = null, IsUnique = idx.Unique, IsPrimary = idx.Primary });
            }

            return AssembleIndexes(rows);
        }

        private static (string Name, bool Unique, bool Primary) ReadIndexListRow(SqlDbQuery query)
        {
            string name = query.GetValue<string>("name");
            bool unique = query.GetValue<int>("unique") != 0;
            string origin = query.GetValue<string>("origin");
            bool primary = string.Equals(origin, "pk", StringComparison.OrdinalIgnoreCase);
            return (name, unique, primary);
        }

        private static bool AddIndexInfoRow(List<RawIndexColumn> rows, SqlDbQuery query, (string Name, bool Unique, bool Primary) idx)
        {
            // PRAGMA index_info columns: 0=seqno, 1=cid, 2=name (NULL for an expression key part)
            string col = query.IsNull(2) ? null : query.GetValue<string>(2);
            rows.Add(new RawIndexColumn { IndexName = idx.Name, Column = col, IsUnique = idx.Unique, IsPrimary = idx.Primary });
            return true;
        }

        public override AlterTableQueryBuilder GetAlterTableQueryBuilder()
        {
            return new SqliteAlterTableQueryBuilder(GetLanguageSpecifics());
        }

        public override SqlDbTransaction BeginTransaction()
        {
            if (mCurrentTransaction != null)
                return new SqliteDbTransaction(this);
            else
            {
                mCurrentTransaction = new SqliteDbTransaction(this, mSqlConnection.BeginTransaction());
                return mCurrentTransaction;
            }
        }

        public override SqlDbTransaction BeginTransaction(IsolationLevel level)
        {
            if (mCurrentTransaction != null)
                throw new EfSqlException(EfExceptionCode.FeatureNotSupported, "The isolation level cannot be set of nested transactions");
            else
            {
                mCurrentTransaction = new SqliteDbTransaction(this, mSqlConnection.BeginTransaction(level));
                return mCurrentTransaction;
            }
        }

        internal void EndTransaction(SqliteDbTransaction transaction)
        {
            if (!transaction.IsSavePoint)
                mCurrentTransaction = null;
        }

        public override DropIndexBuilder GetDropIndexBuilder(TableDescriptor descriptor, string name)
        {
            return new SqliteDropIndexBuilder(GetLanguageSpecifics(), descriptor.Name, name);
        }

        protected async override ValueTask<bool> DoesObjectExistCore(string tableName, string objectName, string objectType, bool executeAsync)
        {
            string query;
            if (objectType == "index")
            {
                query = $"SELECT NAME FROM SQLITE_MASTER WHERE TYPE='index' and NAME='{tableName}_{objectName}';";
            }
            else if (objectType == "table")
            {
                query = $"SELECT NAME FROM SQLITE_MASTER WHERE TYPE='table' and NAME='{tableName}';";
            }
            else if (objectType == "view")
            {
                query = $"SELECT NAME FROM SQLITE_MASTER WHERE TYPE='view' and NAME='{tableName}';";
            }
            else if (objectType == "column")
            {
                query = $"SELECT * FROM PRAGMA_TABLE_INFO('{tableName}') WHERE NAME='{objectName}';";
            }
            else
                throw new ArgumentException($"Unexpected type {objectType}", nameof(objectType));

            using (var stmt = GetQuery(query, true))
            {
                if (executeAsync)
                    await stmt.ExecuteReaderAsync();
                else
                    stmt.ExecuteReader();

                if (executeAsync)
                    return await stmt.ReadNextAsync();
                else
                    return stmt.ReadNext();
            }
        }
    }

    public static class SqliteDbConnectionFactory
    {
        public static bool IncludeVersion { get; set; } = false;

        public static SqlDbConnection Create(string connectionString)
        {
            Microsoft.Data.Sqlite.SqliteConnection connection = new Microsoft.Data.Sqlite.SqliteConnection();
            if (IncludeVersion && !connectionString.Contains("Version="))
                connectionString += ";Version=3;";
            connection.ConnectionString = connectionString;
            connection.Open();
            return new SqliteDbConnection(connection);
        }

        public static SqlDbConnection CreateMemory()
        {
            return Create("Data Source=:memory:");
        }

        public static SqlDbConnection CreateFile(string file, bool createNew, string password = null)
        {
            if (createNew && File.Exists(file))
                File.Delete(file);

            StringBuilder connectionString = new StringBuilder($"Data Source={file};");

#if !NETCORE
            if (createNew)
                connectionString.Append("New=True;");
#endif

            if (password != null)
                connectionString.Append("Password=").Append(password).Append(';');

            return Create(connectionString.ToString());
        }

        public static async Task<SqlDbConnection> CreateAsync(string connectionString, CancellationToken? token)
        {
            Microsoft.Data.Sqlite.SqliteConnection connection = new Microsoft.Data.Sqlite.SqliteConnection();
            if (IncludeVersion && !connectionString.Contains("Version="))
                connectionString += ";Version=3;";
            connection.ConnectionString = connectionString;
            if (token == null)
                await connection.OpenAsync();
            else
                await connection.OpenAsync(token.Value);
            return new SqliteDbConnection(connection);
        }
    }
}