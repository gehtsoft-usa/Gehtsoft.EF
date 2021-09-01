using System;
using System.Collections.Generic;
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

            [ObsoleteEntityProperty(ForeignKey = true)]
            public Entity2Obsolete ObsoleteFK { get; set; }
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

        [Entity(Scope = "finder3", View = true)]
        public class Entity3View
        {
            [AutoId]
            public int ID { get; set; }
        }

        [Entity(Scope = "finder3")]
        public class Entity3Table1
        {
            [AutoId]
            public int ID { get; set; }
            [ForeignKey]
            public Entity3Table2 Key { get; set; }
        }

        [Entity(Scope = "finder3")]
        public class Entity3Table2
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
        public void ProperOrder_Obsolete()
        {
            var entities = EntityFinder.FindEntities(new Assembly[] { typeof(Entity2).Assembly }, "finder2", true);
            EntityFinder.ArrageEntities(entities);
            var e1 = entities.First(eti => eti.EntityType == typeof(Entity2));
            var e2 = entities.First(eti => eti.EntityType == typeof(Entity2Obsolete));
            entities.Should().HaveOneElementAfterTheOther(e2, e1);
        }

        [Fact]
        public void ProperOrder_ViewLast()
        {
            var entities = EntityFinder.FindEntities(new Assembly[] { typeof(Entity2).Assembly }, "finder3", true);

            EntityFinder.ArrageEntities(entities);

            entities[0].EntityType.Should().Be(typeof(Entity3Table2));
            entities[1].EntityType.Should().Be(typeof(Entity3Table1));
            entities[2].EntityType.Should().Be(typeof(Entity3View));
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

        public class DynamicEntity1 : DynamicEntity
        {
            public override EntityAttribute EntityAttribute => new EntityAttribute() { Scope = "finder4" };

            protected override IEnumerable<IDynamicEntityProperty> InitializeProperties()
            {
                yield return new DynamicEntityProperty()
                {
                    PropertyType = typeof(int),
                    Name = "Id",
                    EntityPropertyAttribute = new EntityPropertyAttribute()
                    {
                        DbType = DbType.Int32,
                        PrimaryKey = true,
                        Autoincrement = true,
                        AutoId = true,
                    }
                };
            }
        }

        public class DynamicEntity2 : DynamicEntity
        {
            public override EntityAttribute EntityAttribute => new EntityAttribute() { Scope = "finder4" };

            protected override IEnumerable<IDynamicEntityProperty> InitializeProperties()
            {
                yield return new DynamicEntityProperty()
                {
                    PropertyType = typeof(int),
                    Name = "Id",
                    EntityPropertyAttribute = new EntityPropertyAttribute()
                    {
                        DbType = DbType.Int32,
                        PrimaryKey = true,
                        Autoincrement = true,
                        AutoId = true,
                    }
                };
            }
        }

        public class DynamicEntity3 : DynamicEntity
        {
            public override EntityAttribute EntityAttribute => new EntityAttribute() { Scope = "finder4" };

            protected override IEnumerable<IDynamicEntityProperty> InitializeProperties()
            {
                yield return new DynamicEntityProperty()
                {
                    PropertyType = typeof(int),
                    Name = "Id",
                    EntityPropertyAttribute = new EntityPropertyAttribute()
                    {
                        DbType = DbType.Int32,
                        PrimaryKey = true,
                        Autoincrement = true,
                        AutoId = true,
                    }
                };

                yield return new DynamicEntityProperty()
                {
                    PropertyType = typeof(DynamicEntity1),
                    Name = "Ref",
                    EntityPropertyAttribute = new EntityPropertyAttribute()
                    {
                        DbType = DbType.Int32,
                        ForeignKey = true,
                    }
                };
            }
        }

        static TestEntityFinder()
        {
            AllEntities.EnableDynamicEntityDiscoveryInEntityFinder();
        }

        [Fact]
        public void FindsDynamic()
        {
            var entities = EntityFinder.FindEntities(new Assembly[] { GetType().Assembly }, "finder4", false);

            entities.Should().HaveCount(3);
            entities.Should().HaveElementMatching(m => m.EntityType == typeof(DynamicEntity1));
            entities.Should().HaveElementMatching(m => m.EntityType == typeof(DynamicEntity2));
            entities.Should().HaveElementMatching(m => m.EntityType == typeof(DynamicEntity3));
        }

        [Theory]
        [InlineData(typeof(Order), typeof(Customer), true, "northwind")]
        [InlineData(typeof(Order), typeof(Employee), true, "northwind")]
        [InlineData(typeof(EmployeeTerritory), typeof(Employee), true, "northwind")]
        [InlineData(typeof(Order), typeof(Shipper), true, "northwind")]
        [InlineData(typeof(Employee), typeof(Order), false, "northwind")]

        [InlineData(typeof(DynamicEntity1), typeof(DynamicEntity2), false, "finder4")]
        [InlineData(typeof(DynamicEntity1), typeof(DynamicEntity3), false, "finder4")]
        [InlineData(typeof(DynamicEntity3), typeof(DynamicEntity1), true, "finder4")]
        public void DependsOn(Type type1, Type type2, bool dependsOn, string scope)
        {
            var entities = EntityFinder.FindEntities(new Assembly[] { GetType().Assembly, typeof(Order).Assembly }, scope, false);

            var e1 = entities.First(e => e.EntityType == type1);
            var e2 = entities.First(e => e.EntityType == type2);

            e1.DoesDependOn(e2).Should().Be(dependsOn);
        }
    }
}
