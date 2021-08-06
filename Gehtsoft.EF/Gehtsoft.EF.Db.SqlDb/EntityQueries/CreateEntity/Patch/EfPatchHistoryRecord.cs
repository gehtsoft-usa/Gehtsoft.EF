using System;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.CreateEntity.Patch
{
    [Entity(Table = "ef_patch_history", Scope = "ef_patches")]
    public class EfPatchHistoryRecord
    {
        [AutoId]
        public int ID { get; set; }
        
        [EntityProperty(Sorted = true, Size = 32)]
        public string Scope { get; set; }

        [EntityProperty(Sorted = true)]
        public int MajorVersion { get; set; }

        [EntityProperty(Sorted = true)]
        public int MinorVersion { get; set; }

        [EntityProperty(Sorted = true)]
        public int PatchVersion { get; set; }

        [EntityProperty(Sorted = true)]
        public DateTime Applied { get; set; }

        public EfPatchHistoryRecord()
        {
        }

        public EfPatchHistoryRecord(string scope, int majorVersion, int minorVersion, int patchVersion, DateTime applied)
        {
            Scope = scope;
            MajorVersion = majorVersion;
            MinorVersion = minorVersion;
            PatchVersion = patchVersion;
            Applied = applied;
        }
    }
}
