using System;
using System.IO;
using System.Runtime.InteropServices;
using Gehtsoft.EF.Db.SqliteDb;
using Xunit;

namespace Gehtsoft.EF.Test.Geo
{
    /// <summary>
    /// Shared helpers for behavioural tests that need a live SQLite + SpatiaLite database. The native
    /// library comes from the Spatialite.Native package; tests skip when it is unavailable. Callers must
    /// run in <c>[Collection("SpatialiteSqlite")]</c> so they do not race through the global
    /// enable-spatial flag.
    /// </summary>
    internal static class SpatialiteTestSupport
    {
        public static string LocateLibrary()
        {
            string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
                      : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "linux";
            string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64"
                        : RuntimeInformation.ProcessArchitecture == Architecture.X86 ? "x86" : "x64";
            string file = os == "win" ? "mod_spatialite.dll" : os == "osx" ? "mod_spatialite.dylib" : "mod_spatialite.so";
            string path = Path.Combine(AppContext.BaseDirectory, "runtimes", $"{os}-{arch}", "native", file);
            return File.Exists(path) ? path : null;
        }

        /// <summary>
        /// Runs <paramref name="body"/> against a fresh in-memory SpatiaLite-enabled connection, restoring
        /// the global enable-spatial flags afterwards. Skips the test when the native library is absent.
        /// </summary>
        public static void RunWithSpatialite(Action<SqliteDbConnection> body)
        {
            string library = LocateLibrary();
            if (library == null)
                Assert.Skip("The SpatiaLite native library (Spatialite.Native) is not available for this OS/architecture.");

            bool previousEnabled = SqliteGlobalOptions.EnableSpatial;
            string previousLibrary = SqliteGlobalOptions.SpatialiteLibrary;
            SqliteGlobalOptions.SpatialiteLibrary = library;
            SqliteGlobalOptions.EnableSpatial = true;
            try
            {
                using SqliteDbConnection connection = (SqliteDbConnection)SqliteDbConnectionFactory.CreateMemory();
                body(connection);
            }
            finally
            {
                SqliteGlobalOptions.EnableSpatial = previousEnabled;
                SqliteGlobalOptions.SpatialiteLibrary = previousLibrary;
            }
        }
    }
}
