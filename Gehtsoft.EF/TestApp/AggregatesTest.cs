using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityGenericAccessor;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using NUnit.Framework;

namespace TestApp
{
    public static class AggregatedEntity1ArrExt
    {
        public static AggregatesTest.AggregatedEntity1[] CloneArray(this AggregatesTest.AggregatedEntity1[] arr)
        {
            AggregatesTest.AggregatedEntity1[]rc = new AggregatesTest.AggregatedEntity1[arr.Length];
            for (int i = 0; i < rc.Length; i++)
                rc[i] = arr[i].Clone();
            return rc;
        }
    }


    public static class AggregatesTest
    {
        public interface IEntityBase
        {
            int ID { get; }
            string Name { get; }
        }



        [Entity(Table = "aggregating", Scope = "aggregatesTest")]
        public class AggregatingEntity : IEntityBase
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field="name", Size = 32)]
            public string Name { get; set; }
        }

        public class AggregatingEntityFilter : GenericEntityAccessorFilterT<AggregatingEntity>
        {
            [FilterProperty]
            public string Name { get; set; }

            [FilterProperty(PropertyName = "Name", Operation = CmpOp.Neq)]
            public string NotName { get; set; }
        }

        [Entity(Table = "aggregated1", Scope = "aggregatesTest")]
        public class AggregatedEntity1 : IEntityBase
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(ForeignKey = true)]
            public AggregatingEntity Container { get; set; }

            [EntityProperty(Field="name", Size = 32)]
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

        [Entity(Table = "aggregated2", Scope = "aggregatesTest")]
        public class AggregatedEntity2 : IEntityBase
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(ForeignKey = true)]
            public AggregatingEntity Container { get; set; }

            [EntityProperty(Field="name", Size = 32)]
            public string Name { get; set; }
            
        }

        [Entity(Table = "referring", Scope = "aggregatesTest")]
        public class ReferringEntity : IEntityBase
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(ForeignKey = true)]
            public AggregatingEntity Referred { get; set; }

            [EntityProperty(Field="name", Size = 32)]
            public string Name { get; set; }
            
        }

        private static bool compareContent<A>(A a, A b) where A : IEntityBase
        {
            return (a.ID == b.ID && a.Name == b.Name);
        }

        private static bool compareID<A>(A a, A b) where A : IEntityBase
        {
            return (a.ID == b.ID);
        }

        private static bool isNew<A>(A a) where A : IEntityBase
        {
            return a.ID < 1;
        }

        private static bool isDefined<A>(A a) where A : IEntityBase
        {
            return a.Name != null;
        }

        private static int CreateAgg(GenericEntityAccessorWithAggregates<AggregatingEntity, int> aggregatingAccessor, int id)
        {
            AggregatingEntity agg;
            AggregatedEntity1[] contained1;
            AggregatedEntity2[] contained2;

            agg = new AggregatingEntity() {Name = $"agg{id}"};
            
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
            Assert.AreEqual(contained1.Length, aggregatingAccessor.SaveAggregates<AggregatedEntity1>(agg, new AggregatedEntity1[] {}, contained1, compareContent<AggregatedEntity1>, compareID<AggregatedEntity1>, isDefined<AggregatedEntity1>, isNew<AggregatedEntity1>));
            Assert.AreEqual(contained2.Length, aggregatingAccessor.SaveAggregates<AggregatedEntity2>(agg, new AggregatedEntity2[] {}, contained2, compareContent<AggregatedEntity2>, compareID<AggregatedEntity2>, isDefined<AggregatedEntity2>, isNew<AggregatedEntity2>));

            return agg.ID;
        }

        public static void Do(SqlDbConnection connection)
        {
            CreateEntityController controller = new CreateEntityController(typeof(AggregatesTest), "aggregatesTest");
            controller.DropTables(connection);
            controller.CreateTables(connection);

            GenericEntityAccessorWithAggregates<AggregatingEntity, int> aggregatingAccessor = new GenericEntityAccessorWithAggregates<AggregatingEntity, int>(connection, new Type[] {typeof(AggregatedEntity1), typeof(AggregatedEntity2)});
            GenericEntityAccessor<AggregatingEntity, int> aggregatingAccessorBase = new GenericEntityAccessor<AggregatingEntity, int>(connection);
            GenericEntityAccessor<ReferringEntity, int> referringAccessor = new GenericEntityAccessor<ReferringEntity, int>(connection);
            GenericEntityAccessor<AggregatedEntity1, int> aggregatedAccessor1 = new GenericEntityAccessor<AggregatedEntity1, int>(connection);
            GenericEntityAccessor<AggregatedEntity2, int> aggregatedAccessor2 = new GenericEntityAccessor<AggregatedEntity2, int>(connection);

            int agg1 = CreateAgg(aggregatingAccessor, 1);
            int agg2 = CreateAgg(aggregatingAccessor, 2);
            int agg3 = CreateAgg(aggregatingAccessor, 3);
            int agg4 = CreateAgg(aggregatingAccessor, 4);

            Assert.AreEqual(4, aggregatingAccessor.Count(null));
            Assert.AreEqual(5, aggregatingAccessor.GetAggregatesCount<AggregatedEntity1>(new AggregatingEntity() {ID = agg1}, null));
            Assert.AreEqual(3, aggregatingAccessor.GetAggregatesCount<AggregatedEntity2>(new AggregatingEntity() {ID = agg1}, null));
            
            AggregatedEntity1[] orgData, newData, checkData;

            orgData = aggregatingAccessor.GetAggregates<EntityCollection<AggregatedEntity1>, AggregatedEntity1>(new AggregatingEntity() {ID = agg4}, null, null, null, null).ToArray();
            Assert.IsTrue(orgData != null && orgData.Length > 4);
            newData = orgData.CloneArray();

            int removed = newData[1].ID;
            newData[1].Name = null;
            int updated = newData[2].ID;
            newData[2].Name = "newname";
            int replaced = newData[3].ID;
            newData[3].ID = 0;
            newData[3].Name = "replacedname";

            Assert.AreEqual(4, aggregatingAccessor.SaveAggregates<AggregatedEntity1>(new AggregatingEntity() {ID = agg4}, orgData, newData, compareContent<AggregatedEntity1>, compareID<AggregatedEntity1>, isDefined<AggregatedEntity1>, isNew<AggregatedEntity1>));
            checkData = aggregatingAccessor.GetAggregates<EntityCollection<AggregatedEntity1>, AggregatedEntity1>(new AggregatingEntity() {ID = agg4}, null, null, null, null).ToArray();
            Assert.AreEqual(orgData.Length - 1, checkData.Length);
            for (int i = 0; i < orgData.Length; i++)
            {
                if (orgData[i].ID == removed)
                    Assert.IsNull(aggregatedAccessor1.Get(orgData[i].ID));
                else if (orgData[i].ID == updated)
                    Assert.IsTrue(compareContent<AggregatedEntity1>(newData[i], aggregatedAccessor1.Get(orgData[i].ID)));
                else if (orgData[i].ID == replaced)
                {
                    Assert.IsNull(aggregatedAccessor1.Get(orgData[i].ID));
                    Assert.IsTrue(compareContent<AggregatedEntity1>(newData[i], aggregatedAccessor1.Get(newData[i].ID)));
                }
                else 
                    Assert.IsTrue(compareContent<AggregatedEntity1>(orgData[i], aggregatedAccessor1.Get(orgData[i].ID)));
            }

            int cc1 = aggregatedAccessor1.Count(null);
            int cc2 = aggregatedAccessor1.Count(new AggregatedEntity1Filter() {Container =  new AggregatingEntity() {ID = agg4}});

            Assert.IsTrue(aggregatingAccessor.CanDelete(new AggregatingEntity() {ID = agg4}));
            Assert.IsFalse(aggregatingAccessorBase.CanDelete(new AggregatingEntity() {ID = agg4}));
            aggregatingAccessor.Delete(new AggregatingEntity() {ID = agg4});
            Assert.AreEqual(cc1 - cc2, aggregatedAccessor1.Count(null));
            Assert.AreEqual(0, aggregatedAccessor1.Count(new AggregatedEntity1Filter() {Container = new AggregatingEntity() {ID = agg4}}));

            cc1 = aggregatedAccessor1.Count(new AggregatedEntity1Filter() {Container =  new AggregatingEntity() {ID = agg1}});
            aggregatingAccessor.DeleteMultiple(new AggregatingEntityFilter() {NotName = "agg1"});
            Assert.AreEqual(cc1, aggregatedAccessor1.Count(null));
            Assert.AreEqual(1, aggregatingAccessor.Count(null));

            ReferringEntity re = new ReferringEntity() {Name = "123", Referred = new AggregatingEntity() {ID = agg1}};
            referringAccessor.Save(re);
            Assert.IsFalse(aggregatingAccessor.CanDelete(new AggregatingEntity() {ID = agg1}));
            referringAccessor.Delete(re);
            Assert.IsTrue(aggregatingAccessor.CanDelete(new AggregatingEntity() {ID = agg1}));

            controller.DropTables(connection);
        }
    }
}

