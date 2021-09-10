using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.CreateEntity.Patch
{
    [AttributeUsage(AttributeTargets.Class)]
    public class EfPatchAttribute : Attribute
    {
        public string Scope { get; set; } = "default";
        public int MajorVersion { get; set; }
        public int MinorVersion { get; set; }
        public int PatchVersion { get; set; }

        [ExcludeFromCodeCoverage]
        public EfPatchAttribute()
        {
        }

        [ExcludeFromCodeCoverage]
        public EfPatchAttribute(int major, int minor, int patch)
        {
            MajorVersion = major;
            MinorVersion = minor;
            PatchVersion = patch;
        }

        public EfPatchAttribute(string scope, int major, int minor, int patch)
        {
            Scope = scope;
            MajorVersion = major;
            MinorVersion = minor;
            PatchVersion = patch;
        }
    }
}
