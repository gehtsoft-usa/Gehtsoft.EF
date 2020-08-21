using Gehtsoft.EF.Entities;
using System;

namespace Gehtsoft.EF.Northwind
{
    [Entity(Table = "nw_employees", Scope = "northwind")]
    public class Employee
    {
        [EntityProperty(Field = "employeeID", PrimaryKey = true, Autoincrement = true)]
        public int EmployeeID { get; set; }

        [EntityProperty(Field = "lastName", Size = 32, Nullable = false, Sorted = true)]
        public string LastName { get; set; }

        [EntityProperty(Field = "firstName", Size = 32, Nullable = false, Sorted = true)]
        public string FirstName { get; set; }

        [EntityProperty(Field = "title", Size = 32, Nullable = false, Sorted = true)]
        public string Title { get; set; }

        [EntityProperty(Field = "titleOfCourtesy", Size = 32, Nullable = true)]
        public string TitleOfCourtesy { get; set; }

        [EntityProperty(Field = "birthDate", DbType = System.Data.DbType.Date, Nullable = false, Sorted = true)]
        public DateTime BirthDate { get; set; }

        [EntityProperty(Field = "hireDate", DbType = System.Data.DbType.Date, Nullable = false, Sorted = true)]
        public DateTime HireDate { get; set; }

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

        [EntityProperty(Field = "homePhone", Size = 32, Nullable = true)]
        public string HomePhone { get; set; }

        [EntityProperty(Field = "notes", Nullable = true)]
        public string Notes { get; set; }

        [EntityProperty(Field = "reportsTo", ForeignKey = true, Nullable = true)]
        public Employee ReportsTo { get; set; }
    }
}