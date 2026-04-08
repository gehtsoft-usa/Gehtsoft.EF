using System;
using System.Linq;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityGenericAccessor;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Legacy
{
    public class AggregatesTests : IClassFixture<AggregatesTests.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public AggregatesTests(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = "")
            => SqlConnectionSources.SqlConnectionNames(flags);

        public interface IEntityBase
        {
            int ID { get; }
            string Name { get; }
        }

        [Entity(Table = "laggregating", Scope = "legacyAggregatesTest")]
        public class AggregatingEntity : IEntityBase
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "name", Size = 32)]
            public string Name { get; set; }
        }

        public class AggregatingEntityFilter : GenericEntityAccessorFilterT<AggregatingEntity>
        {
            [FilterProperty]
            public string Name { get; set; }

            [FilterProperty(PropertyName = "Name", Operation = CmpOp.Neq)]
            public string NotName { get; set; }
        }

        [Entity(Table = "laggregated1", Scope = "legacyAggregatesTest")]
        public class AggregatedEntity1 : IEntityBase
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(ForeignKey = true)]
            public AggregatingEntity Container { get; set; }

            [EntityProperty(Field = "name", Size = 32)]
            public string Name { get; set; }

            public AggregatedEntity1 Clone()
            {
                return new AggregatedEntity1()
                {
                    ID = this.ID,
                    Name = this.Name
                };
            }
        }

        public class AggregatedEntity1Filter : GenericEntityAccessorFilterT<AggregatedEntity1>
        {
            [FilterProperty]
            public string Name { get; set; }
            [FilterProperty]
            public AggregatingEntity Container { get; set; }
        }

        [Entity(Table = "laggregated2", Scope = "legacyAggregatesTest")]
        public class AggregatedEntity2 : IEntityBase
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(ForeignKey = true)]
            public AggregatingEntity Container { get; set; }

            [EntityProperty(Field = "name", Size = 32)]
            public string Name { get; set; }
        }

        [Entity(Table = "lreferring", Scope = "legacyAggregatesTest")]
        public class ReferringEntity : IEntityBase
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(ForeignKey = true)]
            public AggregatingEntity Referred { get; set; }

            [EntityProperty(Field = "name", Size = 32)]
            public string Name { get; set; }
        }

        private static bool CompareContent<A>(A a, A b) where A : IEntityBase
        {
            return a.ID == b.ID && a.Name == b.Name;
        }

        private static bool CompareID<A>(A a, A b) where A : IEntityBase
        {
            return a.ID == b.ID;
        }

        private static bool IsNew<A>(A a) where A : IEntityBase
        {
            return a.ID < 1;
        }

        private static bool IsDefined<A>(A a) where A : IEntityBase
        {
            return a.Name != null;
        }

        private static int CreateAgg(GenericEntityAccessorWithAggregates<AggregatingEntity, int> aggregatingAccessor, int id)
        {
            AggregatingEntity agg;
            AggregatedEntity1[] contained1;
            AggregatedEntity2[] contained2;

            agg = new AggregatingEntity() { Name = $"agg{id}" };

            contained1 = new AggregatedEntity1[]
            {
                new AggregatedEntity1() { Name = $"agg{id}.1.1"},
                new AggregatedEntity1() { Name = $"agg{id}.1.2"},
                new AggregatedEntity1() { Name = $"agg{id}.1.3"},
                new AggregatedEntity1() { Name = $"agg{id}.1.4"},
                new AggregatedEntity1() { Name = $"agg{id}.1.5"},
            };

            contained2 = new AggregatedEntity2[]
            {
                new AggregatedEntity2() { Name = $"agg{id}.2.1"},
                new AggregatedEntity2() { Name = $"agg{id}.2.2"},
                new AggregatedEntity2() { Name = $"agg{id}.2.3"},
            };

            aggregatingAccessor.Save(agg);
            aggregatingAccessor.SaveAggregates<AggregatedEntity1>(agg, Array.Empty<AggregatedEntity1>(), contained1, CompareContent<AggregatedEntity1>, CompareID<AggregatedEntity1>, IsDefined<AggregatedEntity1>, IsNew<AggregatedEntity1>).Should().Be(contained1.Length);
            aggregatingAccessor.SaveAggregates<AggregatedEntity2>(agg, Array.Empty<AggregatedEntity2>(), contained2, CompareContent<AggregatedEntity2>, CompareID<AggregatedEntity2>, IsDefined<AggregatedEntity2>, IsNew<AggregatedEntity2>).Should().Be(contained2.Length);

            return agg.ID;
        }

        private static AggregatedEntity1[] CloneArray(AggregatedEntity1[] arr)
        {
            AggregatedEntity1[] rc = new AggregatedEntity1[arr.Length];
            for (int i = 0; i < rc.Length; i++)
                rc[i] = arr[i].Clone();
            return rc;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-oracle")]
        public void AggregateOperations(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            CreateEntityController controller = new CreateEntityController(typeof(AggregatesTests), "legacyAggregatesTest");
            controller.DropTables(connection);
            controller.CreateTables(connection);

            GenericEntityAccessorWithAggregates<AggregatingEntity, int> aggregatingAccessor = new GenericEntityAccessorWithAggregates<AggregatingEntity, int>(connection, new Type[] { typeof(AggregatedEntity1), typeof(AggregatedEntity2) });
            GenericEntityAccessor<AggregatingEntity, int> aggregatingAccessorBase = new GenericEntityAccessor<AggregatingEntity, int>(connection);
            GenericEntityAccessor<ReferringEntity, int> referringAccessor = new GenericEntityAccessor<ReferringEntity, int>(connection);
            GenericEntityAccessor<AggregatedEntity1, int> aggregatedAccessor1 = new GenericEntityAccessor<AggregatedEntity1, int>(connection);

            int agg1 = CreateAgg(aggregatingAccessor, 1);
            int agg4 = CreateAgg(aggregatingAccessor, 4);

            aggregatingAccessor.Count(null).Should().Be(2);
            aggregatingAccessor.GetAggregatesCount<AggregatedEntity1>(new AggregatingEntity() { ID = agg1 }, null).Should().Be(5);
            aggregatingAccessor.GetAggregatesCount<AggregatedEntity2>(new AggregatingEntity() { ID = agg1 }, null).Should().Be(3);

            AggregatedEntity1[] orgData, newData, checkData;

            orgData = aggregatingAccessor.GetAggregates<EntityCollection<AggregatedEntity1>, AggregatedEntity1>(new AggregatingEntity() { ID = agg4 }, null, null, null, null).ToArray();
            (orgData != null && orgData.Length > 4).Should().BeTrue();
            newData = CloneArray(orgData);

            int removed = newData[1].ID;
            newData[1].Name = null;
            int updated = newData[2].ID;
            newData[2].Name = "newname";
            int replaced = newData[3].ID;
            newData[3].ID = 0;
            newData[3].Name = "replacedname";

            aggregatingAccessor.SaveAggregates<AggregatedEntity1>(new AggregatingEntity() { ID = agg4 }, orgData, newData, CompareContent<AggregatedEntity1>, CompareID<AggregatedEntity1>, IsDefined<AggregatedEntity1>, IsNew<AggregatedEntity1>).Should().Be(4);
            checkData = aggregatingAccessor.GetAggregates<EntityCollection<AggregatedEntity1>, AggregatedEntity1>(new AggregatingEntity() { ID = agg4 }, null, null, null, null).ToArray();
            checkData.Length.Should().Be(orgData.Length - 1);
            for (int i = 0; i < orgData.Length; i++)
            {
                if (orgData[i].ID == removed)
                    aggregatedAccessor1.Get(orgData[i].ID).Should().BeNull();
                else if (orgData[i].ID == updated)
                    CompareContent<AggregatedEntity1>(newData[i], aggregatedAccessor1.Get(orgData[i].ID)).Should().BeTrue();
                else if (orgData[i].ID == replaced)
                {
                    aggregatedAccessor1.Get(orgData[i].ID).Should().BeNull();
                    CompareContent<AggregatedEntity1>(newData[i], aggregatedAccessor1.Get(newData[i].ID)).Should().BeTrue();
                }
                else
                    CompareContent<AggregatedEntity1>(orgData[i], aggregatedAccessor1.Get(orgData[i].ID)).Should().BeTrue();
            }

            int cc1 = aggregatedAccessor1.Count(null);
            int cc2 = aggregatedAccessor1.Count(new AggregatedEntity1Filter() { Container = new AggregatingEntity() { ID = agg4 } });

            aggregatingAccessor.CanDelete(new AggregatingEntity() { ID = agg4 }).Should().BeTrue();
            aggregatingAccessorBase.CanDelete(new AggregatingEntity() { ID = agg4 }).Should().BeFalse();
            aggregatingAccessor.Delete(new AggregatingEntity() { ID = agg4 });
            aggregatedAccessor1.Count(null).Should().Be(cc1 - cc2);
            aggregatedAccessor1.Count(new AggregatedEntity1Filter() { Container = new AggregatingEntity() { ID = agg4 } }).Should().Be(0);

            cc1 = aggregatedAccessor1.Count(new AggregatedEntity1Filter() { Container = new AggregatingEntity() { ID = agg1 } });
            aggregatingAccessor.DeleteMultiple(new AggregatingEntityFilter() { NotName = "agg1" });
            aggregatedAccessor1.Count(null).Should().Be(cc1);
            aggregatingAccessor.Count(null).Should().Be(1);

            ReferringEntity re = new ReferringEntity() { Name = "123", Referred = new AggregatingEntity() { ID = agg1 } };
            referringAccessor.Save(re);
            aggregatingAccessor.CanDelete(new AggregatingEntity() { ID = agg1 }).Should().BeFalse();
            referringAccessor.Delete(re);
            aggregatingAccessor.CanDelete(new AggregatingEntity() { ID = agg1 }).Should().BeTrue();

            controller.DropTables(connection);
        }
    }
}
