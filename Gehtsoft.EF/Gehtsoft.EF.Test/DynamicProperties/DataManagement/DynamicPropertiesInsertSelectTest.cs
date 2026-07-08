using System;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.DataManagement
{
    /// <summary>
    /// INSERT ... SELECT is rejected for an entity that owns dynamic properties: the select produces
    /// column values only and cannot populate the side table. The guard is a driver-independent check,
    /// so SQLite is sufficient.
    /// </summary>
    public class DynamicPropertiesInsertSelectTest
    {
        [Entity(Scope = "dp_isel", Table = "dp_isel_owner")]
        [DynamicProperties]
        public class Owner : IDynamicPropertiesOwner
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }

            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        [Fact]
        public void InsertSelect_DynamicPropertyEntity_ThrowsNotSupported()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();
            using (var q = connection.GetCreateEntityQuery<Owner>())
                q.Execute();

            using (var source = connection.GetSelectEntitiesQuery<Owner>())
            {
                Action act = () =>
                {
                    using (var q = connection.GetInsertSelectEntityQuery<Owner>(source))
                        q.Execute();
                };
                act.Should().Throw<NotSupportedException>();
            }
        }
    }
}
