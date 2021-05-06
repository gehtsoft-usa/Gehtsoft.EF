using FluentAssertions;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using Gehtsoft.Tools.TypeUtils;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp
{
    public static class EntityContextTest
    {
        [Entity]
        public class TestContextEntity
        {
            [AutoId]
            public object ID { get; set; }

            [EntityProperty(Sorted = true, Size = 64)]
            public string Name { get; set; }

            [EntityProperty(Sorted = true, Size = 12, Precision = 2)]
            public double Value { get; set; }
        }

        public static bool Test(IEntityContext context)
        {
            try
            {
                using (var query = context.DropEntity<TestContextEntity>())
                    query.Execute();
                using (var query = context.CreateEntity<TestContextEntity>())
                    query.Execute();
                using (var transaction = context.BeginTransaction())
                {
                    using (var query = context.InsertEntity<TestContextEntity>())
                    {
                        Random r = new Random();
                        for (int i = 0; i < 100; i++)
                        {
                            var e = new TestContextEntity()
                            {
                                Name = "Name " + (i + 1),
                            };
                            if (i == 0)
                                e.Value = 0;
                            else if (i == 1)
                                e.Value = 100;
                            else
                                e.Value = r.NextDouble() * 100;
                            query.Execute(e);
                        }
                    }
                    transaction.Commit();
                }

                using (var query = context.Count<TestContextEntity>())
                    query.GetCount().Should().Be(100);

                EntityCollection<TestContextEntity> collection1, collection2;

                using (var query = context.Select<TestContextEntity>())
                {
                    query.Order.Add(nameof(TestContextEntity.Value));
                    query.Execute();
                    collection1 = query.ReadAll<EntityCollection<TestContextEntity>, TestContextEntity>();
                }

                collection1.Count.Should().Be(100);
                collection1.Should().BeInAscendingOrder(v => v.Value);

                using (var query = context.Select<TestContextEntity>())
                {
                    query.Where.Property(nameof(TestContextEntity.Value)).Ls(50);
                    query.Execute();
                    collection2 = query.ReadAll<EntityCollection<TestContextEntity>, TestContextEntity>();
                }

                collection2.Count.Should().BeLessThan(collection1.Count);
                collection2.Count.Should().BeGreaterThan(0);
                collection2.Should().OnlyContain(e => e.Value < 50);

                using (var query = context.Select<TestContextEntity>())
                {
                    query.Where.Property(nameof(TestContextEntity.Value)).Gt(50);
                    query.Execute();
                    collection2 = query.ReadAll<EntityCollection<TestContextEntity>, TestContextEntity>();
                }

                collection2.Count.Should().BeLessThan(collection1.Count);
                collection2.Count.Should().BeGreaterThan(0);
                collection2.Should().OnlyContain(e => e.Value > 50);

                using (var query = context.Select<TestContextEntity>())
                {
                    query.Where.Property(nameof(TestContextEntity.Name)).Like("Name 1%");
                    query.Execute();
                    collection2 = query.ReadAll<EntityCollection<TestContextEntity>, TestContextEntity>();
                }

                collection2.Count.Should().BeLessThan(collection1.Count);
                collection2.Count.Should().BeGreaterThan(0);
                collection2.Should().OnlyContain(e => e.Name.StartsWith("Name 1"));

                using (var query = context.Select<TestContextEntity>())
                {
                    query.Take = 10;
                    query.Skip = 2;
                    query.Order.Add(nameof(TestContextEntity.Value));
                    query.Execute();
                    collection2 = query.ReadAll<EntityCollection<TestContextEntity>, TestContextEntity>();
                }

                collection2.Count.Should().Be(10);
                for (int i = 0; i < 10; i++)
                    collection2[i].ID.Should().Be(collection1[i + 2].ID);

                var entity = context.Get<TestContextEntity>(collection2[2].ID);
                entity.Should().NotBeNull();
                entity.ID.Should().Be(collection2[2].ID);

                entity.Name = "New Name";
                context.Save<TestContextEntity>(entity);
                entity = context.Get<TestContextEntity>(collection2[2].ID);
                entity.ID.Should().Be(collection2[2].ID);
                entity.Name.Should().Be("New Name");

                using (var query = context.DeleteEntity<TestContextEntity>())
                    query.Execute(entity);

                entity = context.Get<TestContextEntity>(collection2[2].ID);
                entity.Should().BeNull();

                using (var query = context.Count<TestContextEntity>())
                    query.GetCount().Should().Be(99);

                entity = new TestContextEntity()
                {
                    Name = "New Entity",
                    Value = 500,
                };
                context.Save(entity);
                entity.ID.Should().NotBeNull();

                var entity1 = context.Get<TestContextEntity>(entity.ID);
                entity1.Should().NotBeNull();
                entity1.ID.Should().Be(entity.ID);
                entity1.Name.Should().Be(entity.Name);
                entity1.Value.Should().Be(entity.Value);

                using (var query = context.DeleteMultiple<TestContextEntity>())
                {
                    query.Where.Property(nameof(TestContextEntity.Value)).Ls(20);
                    query.Execute();
                }

                using (var query = context.Count<TestContextEntity>())
                {
                    query.Where.Property(nameof(TestContextEntity.Value)).Ls(20);
                    query.Execute();
                    query.GetCount().Should().Be(0);
                }
            }
            finally
            {
                using (var query = context.DropEntity<TestContextEntity>())
                    query.Execute();
            }
            return true;
        }
    }
}