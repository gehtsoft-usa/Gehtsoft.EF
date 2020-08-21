using Gehtsoft.EF.Entities;
using System;

namespace Gehtsoft.EF.Northwind
{
    [Entity(Table = "nw_orders", Scope = "northwind")]
    public class Order
    {
        [EntityProperty(Field = "orderID", PrimaryKey = true, Autoincrement = true)]
        public int OrderID { get; set; }

        [EntityProperty(Field = "customerID", ForeignKey = true, Nullable = false)]
        public Customer Customer { get; set; }

        [EntityProperty(Field = "employeeID", ForeignKey = true, Nullable = false)]
        public Employee Employee { get; set; }

        [EntityProperty(Field = "orderDate", DbType = System.Data.DbType.Date, Sorted = true)]
        public DateTime OrderDate { get; set; }

        [EntityProperty(Field = "requiredDate", DbType = System.Data.DbType.Date, Sorted = true)]
        public DateTime RequiredDate { get; set; }

        [EntityProperty(Field = "shippedDate", DbType = System.Data.DbType.Date, Sorted = true, Nullable = true)]
        public DateTime? ShippedDate { get; set; }

        [EntityProperty(Field = "shipVia", ForeignKey = true, Nullable = false)]
        public Shipper ShipVia { get; set; }

        [EntityProperty(Field = "freight", Size = 16, Precision = 2, Sorted = true)]
        public double Freight { get; set; }

        [EntityProperty(Field = "shipName", Size = 64, Sorted = true, Nullable = false)]
        public string ShipName { get; set; }

        [EntityProperty(Field = "shipAddress", Size = 64, Sorted = true, Nullable = false)]
        public string ShipAddress { get; set; }

        [EntityProperty(Field = "shipCity", Size = 64, Sorted = true, Nullable = false)]
        public string ShipCity { get; set; }

        [EntityProperty(Field = "shipRegion", Size = 64, Sorted = true, Nullable = true)]
        public string ShipRegion { get; set; }

        [EntityProperty(Field = "shipPostalCode", Size = 16, Sorted = true, Nullable = false)]
        public string ShipPostalCode { get; set; }

        [EntityProperty(Field = "shipCountry", Size = 64, Sorted = true, Nullable = false)]
        public string ShipCountry { get; set; }
    }
}