using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestApp
{
    public class NorthwindTest
    {
        public void Test(SqlDbConnection context, int? maxRecords = null)
        {
            Snapshot snapshot = new Snapshot();
            snapshot.Create(context, maxRecords);

            var queriableProvider = new QueryableEntityProvider(new ExistingConnectionFactory(context));

            var orders = queriableProvider.Entities<Order>();
            var details = queriableProvider.Entities<OrderDetail>();

            var maxId = orders.Max(o => o.OrderID);
            maxId.Should().BeGreaterThan(10000);
            using (var query = context.GetInsertEntityQuery<Order>())
            {
                var o = snapshot.Orders[0];
                o.OrderID = -1;
                query.Execute(o);
                o.OrderID.Should().BeGreaterThan(maxId);
            }

            var r1 = orders.Count();
            var r2 = orders.Max(o => o.OrderDate);
            var r3 = orders.Min(o => o.OrderDate);
            var r4 = details.Average(o => o.Quantity);
            var r5 = details.Sum(o => o.UnitPrice);
            var r6 = details.Sum(o => o.UnitPrice * o.Quantity);

            Console.WriteLine("{0} {1} {2} {3} {4} {5}", r1, r2, r3, r4, r5, r6);

            var r21 = orders.Where(o => o.ShippedDate == null).Count();
            var r22 = orders.Where(o => o.ShippedDate != null).Count();
            var r23 = (from o in orders where o.ShippedDate != null select o).Count();

            var ra1 = (from o in orders group o by 1 into g select new { c = g.Count(), m = g.Max(v => v.OrderDate) }).ToArray();

            Console.WriteLine("{0} {1} {2} {3}", r21, r22, r23, ra1);
        }
    }
}