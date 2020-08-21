using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Northwind
{
    [Entity(Table = "nw_shippers", Scope = "northwind")]
    public class Shipper
    {
        [EntityProperty(Field = "shipperID", PrimaryKey = true, Autoincrement = true)]
        public int ShipperID { get; set; }

        [EntityProperty(Field = "companyName", Size = 64, Nullable = false, Sorted = true)]
        public string CompanyName { get; set; }

        [EntityProperty(Field = "phone", Size = 16, Nullable = false)]
        public string Phone { get; set; }
    }
}