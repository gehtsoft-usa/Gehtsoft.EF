using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Northwind
{
    [Entity(Table = "nw_order_details", Scope = "northwind")]
    public class OrderDetail
    {
        [AutoId(Field = "detailID")]
        public int Id { get; set; }

        [EntityProperty(Field = "orderID", ForeignKey = true, Nullable = false)]
        public Order Order { get; set; }

        [EntityProperty(Field = "productID", ForeignKey = true, Nullable = false)]
        public Product Product { get; set; }

        [EntityProperty(Field = "unitPrice", Precision = 2, Size = 12, Sorted = true)]
        public double UnitPrice { get; set; }

        [EntityProperty(Field = "quantity", Precision = 2, Size = 12, Sorted = true)]
        public double Quantity { get; set; }

        [EntityProperty(Field = "discount", Precision = 4, Size = 8, Sorted = true)]
        public double Discount { get; set; }
    }
}