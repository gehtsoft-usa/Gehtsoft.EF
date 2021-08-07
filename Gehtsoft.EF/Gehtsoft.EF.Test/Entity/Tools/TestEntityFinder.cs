using System.Data;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using Gehtsoft.EF.Test.Entity.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Tools
{
    public class TestEntityFinder
    {
        [Entity(Scope = "finder1", Metadata = typeof(TestEntityFinder))]
        public class Entity1
        {
            [AutoId]
            public int ID { get; set; }
        }

        [Entity(Scope = "finder2")]
        public class Entity2
        {
            [AutoId]
            public int ID { get; set; }
        }

        [ObsoleteEntity(Scope = "finder1")]
        public class Entity1Obsolete
        {
            [AutoId]
            public int ID { get; set; }
        }

        [ObsoleteEntity(Scope = "finder2")]
        public class Entity2Obsolete
        {
            [AutoId]
            public int ID { get; set; }
        }

        [Fact]
        public void FindAllScopesInMultipleAssemblies()
        {
            var entities = EntityFinder.FindEntities(
                new Assembly[] { typeof(Entity1).Assembly,
                                 typeof(Order).Assembly }, null, true);
            entities.Should().Contain(eti => eti.EntityType == typeof(Category));
            entities.Should().Contain(eti => eti.EntityType == typeof(Entity1));
            entities.Should().Contain(eti => eti.EntityType == typeof(Entity2));
            entities.Should().Contain(eti => eti.EntityType == typeof(Entity1Obsolete));
            entities.Should().Contain(eti => eti.EntityType == typeof(Entity2Obsolete));
        }

        [Fact]
        public void FindOneScopeNoObsolete()
        {
            var entities = EntityFinder.FindEntities(
                new Assembly[] { typeof(Entity1).Assembly }, "finder1", false);
            entities.Should().Contain(eti => eti.EntityType == typeof(Entity1));
            entities.Should().NotContain(eti => eti.EntityType == typeof(Entity2));
            entities.Should().NotContain(eti => eti.EntityType == typeof(Entity1Obsolete));
            entities.Should().NotContain(eti => eti.EntityType == typeof(Entity2Obsolete));
        }

        [Fact]
        public void FindOneScopeObsolete()
        {
            var entities = EntityFinder.FindEntities(
                new Assembly[] { typeof(Entity1).Assembly }, "finder1", true);
            entities.Should().Contain(eti => eti.EntityType == typeof(Entity1));
            entities.Should().NotContain(eti => eti.EntityType == typeof(Entity2));
            entities.Should().Contain(eti => eti.EntityType == typeof(Entity1Obsolete));
            entities.Should().NotContain(eti => eti.EntityType == typeof(Entity2Obsolete));
        }

        [Fact]
        public void FindInOtherAssembly()
        {
            var entities = EntityFinder.FindEntities(new Assembly[] { typeof(Order).Assembly }, "northwind", false);
            entities.Should().Contain(eti => eti.EntityType == typeof(Category));
            entities.Should().Contain(eti => eti.EntityType == typeof(Customer));
            entities.Should().Contain(eti => eti.EntityType == typeof(Employee));
            entities.Should().Contain(eti => eti.EntityType == typeof(Order));
            entities.Should().Contain(eti => eti.EntityType == typeof(OrderDetail));
            entities.Should().Contain(eti => eti.EntityType == typeof(Product));
            entities.Should().Contain(eti => eti.EntityType == typeof(Region));
            entities.Should().Contain(eti => eti.EntityType == typeof(Shipper));
            entities.Should().Contain(eti => eti.EntityType == typeof(Supplier));
            entities.Should().Contain(eti => eti.EntityType == typeof(Territory));

            entities.Should().NotContain(eti => eti.EntityType == typeof(Entity1));
            entities.Should().NotContain(eti => eti.EntityType == typeof(Entity2));
        }

        [Fact]
        public void ProperOrder_ByKnownSequence()
        {
            var entities = EntityFinder.FindEntities(new Assembly[] { typeof(Order).Assembly }, "northwind", false);
            EntityFinder.ArrageEntities(entities);

            var category = entities.First(eti => eti.EntityType == typeof(Category));
            var customer = entities.First(eti => eti.EntityType == typeof(Customer));
            var employee = entities.First(eti => eti.EntityType == typeof(Employee));
            var employee_territory = entities.First(eti => eti.EntityType == typeof(EmployeeTerritory));
            var order = entities.First(eti => eti.EntityType == typeof(Order));
            var order_detail = entities.First(eti => eti.EntityType == typeof(OrderDetail));
            var product = entities.First(eti => eti.EntityType == typeof(Product));
            var shipper = entities.First(eti => eti.EntityType == typeof(Shipper));
            var supplier = entities.First(eti => eti.EntityType == typeof(Supplier));
            var territory = entities.First(eti => eti.EntityType == typeof(Territory));

            entities.Should().HaveOneElementAfterTheOther(category, product);
            entities.Should().HaveOneElementAfterTheOther(supplier, product);

            entities.Should().HaveOneElementAfterTheOther(employee, employee_territory);
            entities.Should().HaveOneElementAfterTheOther(territory, employee_territory);

            entities.Should().HaveOneElementAfterTheOther(customer, order);
            entities.Should().HaveOneElementAfterTheOther(employee, order);
            entities.Should().HaveOneElementAfterTheOther(shipper, order);

            entities.Should().HaveOneElementAfterTheOther(product, order_detail);
            entities.Should().HaveOneElementAfterTheOther(order, order_detail);

        }

        [Fact]
        public void PassingDataToDescription()
        {
            var entities = EntityFinder.FindEntities(new Assembly[] { typeof(Entity1).Assembly, typeof(Order).Assembly }, null, false);
            var order = entities.First(eti => eti.EntityType == typeof(Order));
            
            order.Table.Should().Be("nw_ord");
            order.Scope.Should().Be("northwind");

            order.DependsOn.Should().Contain(typeof(Customer));
            order.DependsOn.Should().Contain(typeof(Employee));
            order.DependsOn.Should().Contain(typeof(Shipper));

            order.Metadata.Should().BeNull();

            var entity1 = entities.First(eti => eti.EntityType == typeof(Entity1));
            entity1.Metadata.Should().Be(typeof(TestEntityFinder));


        }

        [Fact]
        public void ProperOrder_ByRule()
        {
            var entities = EntityFinder.FindEntities(new Assembly[] { typeof(Order).Assembly }, "northwind", false);
            EntityFinder.ArrageEntities(entities);

            for (int i = 0; i < entities.Length; i++)
            {
                var entityInfo = AllEntities.Get(entities[i].EntityType);
                for (int j = 0; j < entityInfo.TableDescriptor.Count; j++)
                {
                    if (entityInfo.TableDescriptor[j].ForeignKey && 
                        entityInfo.TableDescriptor[j].ForeignTable.Name != entityInfo.TableDescriptor.Name)
                    {
                        int? targetIndex = null;
                        for (int k = 0; k < entities.Length && targetIndex == null; k++)
                            if (entities[k].Table == entityInfo.TableDescriptor[j].ForeignTable.Name)
                                targetIndex = k;

                        targetIndex.Should().NotBeNull();
                        targetIndex.Should().BeLessThan(i, "Foreign key reference must be located before the referring entity");
                    }
                }
            }
        }

        [Fact]
        public void TableDescriptorCreation()
        {
            var entities = EntityFinder.FindEntities(new Assembly[] { typeof(Order).Assembly }, "northwind", false);

        }
    }
}
