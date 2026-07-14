using System;
using System.Collections.Generic;
using System.Text;

namespace Gehtsoft.EF.Db.SqliteDb
{
    public static class SqliteGlobalOptions
    {
        public static bool StoreDateAsString { get; set; } = false;

        /// <summary>
        /// When set, an opened connection loads the SpatiaLite extension and bootstraps its spatial
        /// metadata once per database. Opt-in because the native <c>mod_spatialite</c> library is the
        /// application's responsibility to provide. Off by default.
        /// </summary>
        public static bool EnableSpatial { get; set; } = false;

        /// <summary>
        /// The SpatiaLite native extension to load when <see cref="EnableSpatial"/> is set. Defaults to
        /// <c>mod_spatialite</c>; override to point at a specific library name or path.
        /// </summary>
        public static string SpatialiteLibrary { get; set; } = "mod_spatialite";
    }
}
