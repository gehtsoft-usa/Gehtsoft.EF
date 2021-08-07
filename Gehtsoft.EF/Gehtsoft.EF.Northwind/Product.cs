using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Northwind
{
    [Entity(Table = "nw_prod", Scope = "northwind")]
    public class Product
    {
        [EntityProperty(Field = "productID", PrimaryKey = true, Autoincrement = true)]
        public int ProductID { get; set; }

        [EntityProperty(Field = "productName", Size = 64, Sorted = true, Nullable = false, Unique = true)]
        public string ProductName { get; set; }

        [EntityProperty(Field = "supplierID", ForeignKey = true, Nullable = false)]
        public Supplier Supplier { get; set; }

        [EntityProperty(Field = "categoryID", ForeignKey = true, Nullable = false)]
        public Category Category { get; set; }

        [EntityProperty(Field = "quantityPerUnit", Size = 32, Nullable = false, DefaultValue = "")]
        public string QuantityPerUnit { get; set; }

        [EntityProperty(Field = "unitPrice", Size = 12, Precision = 2, Sorted = true)]
        public double UnitPrice { get; set; }

        [EntityProperty(Field = "unitsInStock", Size = 12, Precision = 2, Sorted = true)]
        public double UnitsInStock { get; set; }

        [EntityProperty(Field = "unitsOnOrder", Size = 12, Precision = 2, Sorted = true)]
        public double UnitsOnOrder { get; set; }

        [EntityProperty(Field = "reorderLevel", Size = 12, Precision = 2, Sorted = true)]
        public double ReorderLevel { get; set; }

        [EntityProperty(Field = "discontinued", Sorted = true, Nullable = false)]
        public bool Discontinued { get; set; }
    }
}