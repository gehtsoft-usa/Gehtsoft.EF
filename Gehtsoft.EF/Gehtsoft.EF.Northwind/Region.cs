using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Northwind
{
    [Entity(Table = "nw_reg", Scope = "northwind")]
    public class Region
    {
        [EntityProperty(Field = "regionID", PrimaryKey = true, Autoincrement = true)]
        public int RegionID { get; set; }

        [EntityProperty(Field = "regionDescription", Size = 64, Nullable = false, Sorted = true)]
        public string RegionDescription { get; set; }
    }
}