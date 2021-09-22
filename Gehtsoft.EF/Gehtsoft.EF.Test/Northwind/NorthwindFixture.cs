using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.OracleDb;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities.Context;
using Gehtsoft.EF.MongoDb;
using Gehtsoft.EF.Northwind;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Northwind
{
    public sealed class NorthwindFixture : SqlConnectionFixtureBase
    {
        public Snapshot Snapshot { get; } = new Snapshot();

        public EntityDescriptor CustomerType { get; } = AllEntities.Get<Customer>();
        public TableDescriptor CustomerTable => CustomerType.TableDescriptor;
        public EntityDescriptor CategoryType { get; } = AllEntities.Get<Category>();
        public TableDescriptor CategoryTable => CategoryType.TableDescriptor;
        public EntityDescriptor EmployeeType { get; } = AllEntities.Get<Employee>();
        public TableDescriptor EmployeeTable => EmployeeType.TableDescriptor;
        public EntityDescriptor EmployeeTerritoryType { get; } = AllEntities.Get<EmployeeTerritory>();
        public TableDescriptor EmployeeTerritoryTable => EmployeeTerritoryType.TableDescriptor;
        public EntityDescriptor OrderType { get; } = AllEntities.Get<Order>();
        public TableDescriptor OrderTable => OrderType.TableDescriptor;
        public EntityDescriptor OrderDetailType { get; } = AllEntities.Get<OrderDetail>();
        public TableDescriptor OrderDetailTable => OrderDetailType.TableDescriptor;
        public EntityDescriptor ProductType { get; } = AllEntities.Get<Product>();
        public TableDescriptor ProductTable => ProductType.TableDescriptor;
        public EntityDescriptor RegionType { get; } = AllEntities.Get<Region>();
        public TableDescriptor RegionTable => RegionType.TableDescriptor;
        public EntityDescriptor ShipperType { get; } = AllEntities.Get<Shipper>();
        public TableDescriptor ShipperTable => ShipperType.TableDescriptor;
        public EntityDescriptor TerritoryType { get; } = AllEntities.Get<Territory>();
        public TableDescriptor TerritoryTable => TerritoryType.TableDescriptor;
        public EntityDescriptor SupplierType { get; } = AllEntities.Get<Supplier>();
        public TableDescriptor SupplierTable => SupplierType.TableDescriptor;

        public NorthwindFixture()
        {
        }

        protected override void ConfigureConnection(SqlDbConnection connection)
        {
            Snapshot.Create(connection, 100);
            if (connection.ConnectionType == UniversalSqlDbFactory.ORACLE &&
                connection is OracleDbConnection oracleConnection)
            {
                oracleConnection.UpdateSequence(typeof(Category));
                oracleConnection.UpdateSequence(typeof(Employee));
                oracleConnection.UpdateSequence(typeof(Product));
                oracleConnection.UpdateSequence(typeof(Region));
                oracleConnection.UpdateSequence(typeof(Order));
                oracleConnection.UpdateSequence(typeof(Shipper));
                oracleConnection.UpdateSequence(typeof(Supplier));
            }
            base.ConfigureConnection(connection);
        }
    }

    [CollectionDefinition(nameof(NorthwindFixture))]
    public class NorthwindFixtureCollection : ICollectionFixture<NorthwindFixture>
    {
    }
}
