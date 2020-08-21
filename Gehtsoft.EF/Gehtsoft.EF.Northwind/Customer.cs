using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Northwind
{
    [Entity(Table = "nw_customers", Scope = "northwind")]
    public class Customer
    {
        [EntityProperty(Field = "customerID", Size = 5, PrimaryKey = true)]
        public string CustomerID { get; set; }

        [EntityProperty(Field = "companyName", Size = 128, Nullable = false, Sorted = true)]
        public string CompanyName { get; set; }

        [EntityProperty(Field = "contactName", Size = 128, Nullable = false, Sorted = true)]
        public string ContactName { get; set; }

        [EntityProperty(Field = "contactTitle", Size = 128)]
        public string ContactTitle { get; set; }

        [EntityProperty(Field = "address", Size = 256)]
        public string Address { get; set; }

        [EntityProperty(Field = "city", Size = 64, Nullable = false, Sorted = true)]
        public string City { get; set; }

        [EntityProperty(Field = "region", Size = 64, Nullable = true, Sorted = true)]
        public string Region { get; set; }

        [EntityProperty(Field = "postalCode", Size = 16, Nullable = false, Sorted = true)]
        public string PostalCode { get; set; }

        [EntityProperty(Field = "country", Size = 64, Nullable = false, Sorted = true)]
        public string Country { get; set; }

        [EntityProperty(Field = "phone", Size = 32, Nullable = true)]
        public string Phone { get; set; }

        [EntityProperty(Field = "fax", Size = 32, Nullable = true)]
        public string Fax { get; set; }
    }
}