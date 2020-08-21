using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TestApp
{
    public class NorthwindTest
    {
        private Snapshot mSnapshot = new Snapshot();

        public void Test(SqlDbConnection context)
        {
            mSnapshot.CreateAsync(context).ConfigureAwait(true).GetAwaiter().GetResult();

            var queriableProvider = new QueryableEntityProvider(new QueryableEntityProviderConnection(context));

            var orders = queriableProvider.Entities<Order>();
            var details = queriableProvider.Entities<OrderDetail>();

            var r1 = orders.Count();
            var r2 = orders.Max(o => o.OrderDate);
            var r3 = orders.Min(o => o.OrderDate);
            var r4 = details.Average(o => o.Quantity);
            var r5 = details.Sum(o => o.UnitPrice);
            var r6 = details.Sum(o => o.UnitPrice * o.Quantity);

            var r21 = orders.Where(o => o.ShippedDate == null).Count();
            var r22 = orders.Where(o => o.ShippedDate != null).Count();
            var r23 = (from o in orders where o.ShippedDate != null select o).Count();

            var ra1 = (from o in orders group o by 1 into g select new { c = g.Count(), m = g.Max(v => v.OrderDate) }).ToArray();
        }
    }
}