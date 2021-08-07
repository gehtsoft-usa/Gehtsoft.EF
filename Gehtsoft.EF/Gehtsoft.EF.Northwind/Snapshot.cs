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
        private readonly (Type type, IEnumerable list)[] mTypes;

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

            mTypes = new (Type type, IEnumerable list)[]
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
        }

        public Task CreateAsync(IEntityContext context, int? orderLimit = null) => CreateCore(context, true, orderLimit).AsTask();

        public void Create(IEntityContext context, int? orderLimit = null)
        {
            if (!CreateCore(context, false, orderLimit).IsCompleted)
                throw new InvalidOperationException("The sync operation is expected to be completed");
        }

        private async ValueTask CreateCore(IEntityContext context, bool executeAsync, int? orderLimit)
        {
            if (executeAsync)
                await CreateTablesAsync(context);
            else
                CreateTables(context);          

            var transaction = context.BeginTransaction();

            try
            {
                HashSet<int> orders = new HashSet<int>();
                
                foreach (var (type, list) in mTypes)
                {
                    bool createKey = type == typeof(OrderDetail) || type == typeof(EmployeeTerritory);
                    
                    bool itIsOrder = type == typeof(Order) && orderLimit != null; 
                    bool itIsOrderDetail = type == typeof(OrderDetail) && orderLimit != null;

                    using (var query = context.InsertEntity(type, createKey))
                    {
                        foreach (var v in list)
                        {
                            if (itIsOrder && orders.Count >= orderLimit.Value)
                                break;

                            if (itIsOrderDetail && v is OrderDetail details)
                                if (!orders.Contains(details.Order.OrderID))
                                    continue;

                            if (executeAsync)
                                await query.ExecuteAsync(v);
                            else
                                query.Execute(v);

                            if (itIsOrder && v is Order order)
                                orders.Add(order.OrderID);

                            

                        }
                    }
                }
                transaction.Commit();

                if (orderLimit != null)
                {
                    List<Order> newOrders = new List<Order>();
                    for (int i = 0; i < orderLimit.Value; i++)
                        newOrders.Add(Orders[i]);
                    Orders = newOrders;
                    
                    List<OrderDetail> newOrderDetails = new List<OrderDetail>();
                    for (int i = 0; i < orderLimit.Value; i++)
                        for (int j = 0; j < OrderDetails.Count; j++)
                            if (OrderDetails[j].Order.OrderID == Orders[i].OrderID)
                                newOrderDetails.Add(OrderDetails[j]);
                    OrderDetails = newOrderDetails;
                }
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

        public Task CreateTablesAsync(IEntityContext context) => CreateTablesCore(context, true).AsTask();

        public void CreateTables(IEntityContext context)
        {
            if (!CreateTablesCore(context, false).IsCompleted)
                throw new InvalidOperationException("The sync operation is expected to be completed");
        }

        private async ValueTask CreateTablesCore(IEntityContext context, bool executeAsync)
        {
            foreach (var (type, list) in mTypes.Reverse())
            {
                using (var query = context.DropEntity(type))
                    if (executeAsync)
                        await query.ExecuteAsync();
                    else
                        query.Execute();
            }

            foreach (var (type, list) in mTypes)
            {
                using (var query = context.CreateEntity(type))
                    if (executeAsync)
                        await query.ExecuteAsync();
                    else
                        query.Execute();
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