using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Northwind
{
    [Entity(Table = "nw_territories", Scope = "northwind")]
    public class Territory
    {
        [EntityProperty(Field = "territoryID", PrimaryKey = true, Size = 5)]
        public string TerritoryID { get; set; }

        [EntityProperty(Field = "territoryDescription")]
        public string TerritoryDescription { get; set; }

        [EntityProperty(Field = "regionID", ForeignKey = true)]
        public Region Region { get; set; }
    }
}