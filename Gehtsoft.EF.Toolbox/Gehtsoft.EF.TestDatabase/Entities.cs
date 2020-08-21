using System;
using System.Data;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.TestDatabase
{
    public enum EmployeeType
    {
        Manager,
        Salesman,
    }

    [Entity(Table = "testdb_employee", Scope = "testdb")]
    public class Employee
    {
        [EntityProperty(AutoId = true)]
        public int ID { get; set; }

        [EntityProperty(Size = 64, Nullable = false, Sorted = true)]
        public string FirstName { get; set; }

        [EntityProperty(Size = 64, Nullable = false, Sorted = true)]
        public string LastName { get; set; }

        [EntityProperty(DbType = DbType.Date, Sorted = true)]
        public DateTime EmployedSince { get; set; }

        [EntityProperty(Sorted = true)]
        public bool Active { get; set; }

        [EntityProperty(Sorted = true, DbType = DbType.Int32)]
        public EmployeeType EmployeeType { get; set; }

        [EntityProperty(ForeignKey = true, Nullable = true)]
        public Employee Manager { get; set; }
    }

    [Entity(Table = "testdb_category", Scope = "testdb")]
    public class Category
    {
        [EntityProperty(AutoId = true)]
        public int ID { get; set; }

        [EntityProperty(Size = 64, Nullable = false, Sorted = true)]
        public string Name { get; set; }

    }

    [Entity(Table = "testdb_good", Scope = "testdb")]
    public class Good
    {
        [EntityProperty(AutoId = true)]
        public int ID { get; set; }

        [EntityProperty(Size = 64, Nullable = false, Sorted = true)]
        public string Name { get; set; }

        [EntityProperty(ForeignKey = true)]
        public Category Category { get; set; }
    }

    [Entity(Table = "testdb_sale", Scope = "testdb")]
    public class Sale
    {
        [EntityProperty(AutoId = true)]
        public int ID { get; set; }

        [EntityProperty(ForeignKey = true)]
        public Good Good { get; set; }

        [EntityProperty(ForeignKey = true)]
        public Employee SoldBy { get; set; }

        [EntityProperty(DbType = DbType.DateTime)]
        public DateTime SaleDate { get; set; }

        [EntityProperty(DbType = DbType.Double, Size = 12, Precision = 2)]
        public double Amount { get; set; }

        [EntityProperty(DbType = DbType.Double, Size = 12, Precision = 2, Nullable = true)]
        public double? SalesTax { get; set; }

        [EntityProperty(Nullable = true, DbType = DbType.Binary)]
        public byte[] Invoice { get; set; }
    }
}
