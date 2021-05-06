using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using Gehtsoft.EF.Northwind.Factory;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Northwind
{
    public class Snapshot
    {
        public Snapshot()
        {
            Customers = (new CsvReader<Customer>()).Read();
            Categories = (new CsvReader<Category>()).Read();
            Regions = (new CsvReader<Region>()).Read();
            Territories = (new CsvReader<Territory>()).Read();
            Employees = (new CsvReader<Employee>()).Read();
            EmployeeTerritories = (new CsvReader<EmployeeTerritory>()).Read();
            Shippers = (new CsvReader<Shipper>()).Read();
            Suppliers = (new CsvReader<Supplier>()).Read();
            Products = (new CsvReader<Product>()).Read();
            Orders = (new CsvReader<Order>()).Read();
            OrderDetails = (new CsvReader<OrderDetail>()).Read();
        }

        public async Task CreateAsync(IEntityContext context)
        {
            (Type type, IEnumerable list)[] types = new (Type type, IEnumerable list)[]
            {
                (typeof(Category), this.Categories),
                (typeof(Region), this.Regions),
                (typeof(Territory), this.Territories),
                (typeof(Shipper), this.Shippers),
                (typeof(Supplier), this.Suppliers),
                (typeof(Customer), this.Customers),
                (typeof(Employee), this.Employees),
                (typeof(EmployeeTerritory), this.EmployeeTerritories),
                (typeof(Product), this.Products),
                (typeof(Order), this.Orders),
                (typeof(OrderDetail), this.OrderDetails),
            };

            foreach (var (type, list) in types.Reverse())
            {
                using (var query = context.DropEntity(type))
                    await query.ExecuteAsync();
            }

            foreach (var (type, list) in types)
            {
                using (var query = context.CreateEntity(type))
                    await query.ExecuteAsync();
            }

            var transaction = context.BeginTransaction();

            try
            {
                foreach (var (type, list) in types)
                {
                    bool createKey = type == typeof(OrderDetail) || type == typeof(EmployeeTerritory);
                    using (var query = context.InsertEntity(type, createKey))
                    {
                        foreach (var v in list)
                            await query.ExecuteAsync(v);
                    }
                }
                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                transaction.Dispose();
            }
        }

        public IReadOnlyList<Customer> Customers { get; internal set; }
        public IReadOnlyList<Category> Categories { get; internal set; }
        public IReadOnlyList<Product> Products { get; internal set; }
        public IReadOnlyList<Region> Regions { get; internal set; }
        public IReadOnlyList<Territory> Territories { get; internal set; }
        public IReadOnlyList<Employee> Employees { get; internal set; }
        public IReadOnlyList<EmployeeTerritory> EmployeeTerritories { get; internal set; }
        public IReadOnlyList<Shipper> Shippers { get; internal set; }
        public IReadOnlyList<Supplier> Suppliers { get; internal set; }
        public IReadOnlyList<Order> Orders { get; internal set; }
        public IReadOnlyList<OrderDetail> OrderDetails { get; internal set; }
    }
}