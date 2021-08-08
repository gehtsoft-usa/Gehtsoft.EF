using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using Moq;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Context
{
    public class TestContextQueryExtensions
    {
        [Entity(Scope = "testcontext")]
        public class TestEntity
        {
            [AutoId]
            public int Id { get; set; }
        }

        [Fact]
        public void Drop()
        {
            var context = new Mock<IEntityContext>(MockBehavior.Strict);
            var query = new Mock<IEntityQuery>();

            context.Setup(c => c.DropEntity(It.Is<Type>(v => v == typeof(TestEntity))))
                .Returns(query.Object)
                .Verifiable();

            var r = context.Object.DropEntity<TestEntity>();
            r.Should().BeSameAs(query.Object);
            context.Verify();
        }

        [Fact]
        public void Create()
        {
            var context = new Mock<IEntityContext>(MockBehavior.Strict);
            var query = new Mock<IEntityQuery>();

            context.Setup(c => c.CreateEntity(It.Is<Type>(v => v == typeof(TestEntity))))
                .Returns(query.Object)
                .Verifiable();

            var r = context.Object.CreateEntity<TestEntity>();
            r.Should().BeSameAs(query.Object);
            context.Verify();
        }

        [Fact]
        public void Insert()
        {
            var context = new Mock<IEntityContext>(MockBehavior.Strict);
            var query = new Mock<IModifyEntityQuery>();

            context.Setup(c => c.InsertEntity(It.Is<Type>(v => v == typeof(TestEntity)), It.Is<bool>(v => v)))
                .Returns(query.Object)
                .Verifiable();

            var r = context.Object.InsertEntity<TestEntity>(true);
            r.Should().BeSameAs(query.Object);
            context.Verify();
        }

        [Fact]
        public void Update()
        {
            var context = new Mock<IEntityContext>(MockBehavior.Strict);
            var query = new Mock<IModifyEntityQuery>();

            context.Setup(c => c.UpdateEntity(It.Is<Type>(v => v == typeof(TestEntity))))
                .Returns(query.Object)
                .Verifiable();

            var r = context.Object.UpdateEntity<TestEntity>();
            r.Should().BeSameAs(query.Object);
            context.Verify();
        }

        [Fact]
        public void Delete()
        {
            var context = new Mock<IEntityContext>(MockBehavior.Strict);
            var query = new Mock<IModifyEntityQuery>();

            context.Setup(c => c.DeleteEntity(It.Is<Type>(v => v == typeof(TestEntity))))
                .Returns(query.Object)
                .Verifiable();

            var r = context.Object.DeleteEntity<TestEntity>();
            r.Should().BeSameAs(query.Object);
            context.Verify();
        }

        [Fact]
        public void DeleteMany()
        {
            var context = new Mock<IEntityContext>(MockBehavior.Strict);
            var query = new Mock<IContextQueryWithCondition>();

            context.Setup(c => c.DeleteMultiple(It.Is<Type>(v => v == typeof(TestEntity))))
                .Returns(query.Object)
                .Verifiable();

            var r = context.Object.DeleteMultiple<TestEntity>();
            r.Should().BeSameAs(query.Object);
            context.Verify();
        }

        [Fact]
        public void Select()
        {
            var context = new Mock<IEntityContext>(MockBehavior.Strict);
            var query = new Mock<IContextSelect>();

            context.Setup(c => c.Select(It.Is<Type>(v => v == typeof(TestEntity))))
                .Returns(query.Object)
                .Verifiable();

            var r = context.Object.Select<TestEntity>();
            r.Should().BeSameAs(query.Object);
            context.Verify();
        }

        [Fact]
        public void Count()
        {
            var context = new Mock<IEntityContext>(MockBehavior.Strict);
            var query = new Mock<IContextCount>();

            context.Setup(c => c.Count(It.Is<Type>(v => v == typeof(TestEntity))))
                .Returns(query.Object)
                .Verifiable();

            var r = context.Object.Count<TestEntity>();
            r.Should().BeSameAs(query.Object);
            context.Verify();
        }

        [Fact]
        public void ReadOne()
        {
            var select = new Mock<IContextSelect>(MockBehavior.Strict);
            var r = new TestEntity();

            select.Setup(s => s.ReadOne())
                .Returns(r)
                .Verifiable();

            var r1 = select.Object.ReadOne<TestEntity>();

            r1.Should().BeSameAs(r);

            select.Verify();
        }

        [Fact]
        public void ReadAll()
        {
            var select = new Mock<IContextSelect>(MockBehavior.Strict);
            int created = 0;

            select.Setup(s => s.ReadOne())
                .Returns(() => created < 10 ? new TestEntity() { Id = ++created } : null)
                .Verifiable();

            var c = select.Object.ReadAll<TestEntity>();

            c.Should().HaveCount(10);
            for (int i = 0; i < 10; i++)
                c[i].Id.Should().Be(i + 1);


            select.Verify(s => s.ReadOne(), Times.Exactly(11));
        }
    }

    public class IContextConditionExtensions
    {
        [Fact]
        public void And()
        {
            var filter = new Mock<IContextFilter>(MockBehavior.Strict);
            var condition = new Mock<IContextFilterCondition>(MockBehavior.Strict);

            filter.Setup(f => f.Add(It.Is<LogOp>(v => v == LogOp.And)))
                .Returns(condition.Object)
                .Verifiable();

            var condition1 = filter.Object.And();
            condition1.Should().BeSameAs(condition.Object);
            filter.Verify();
        }

        [Fact]
        public void Or()
        {
            var filter = new Mock<IContextFilter>(MockBehavior.Strict);
            var condition = new Mock<IContextFilterCondition>(MockBehavior.Strict);

            filter.Setup(f => f.Add(It.Is<LogOp>(v => v == LogOp.Or)))
                .Returns(condition.Object)
                .Verifiable();

            var condition1 = filter.Object.Or();
            condition1.Should().BeSameAs(condition.Object);
            filter.Verify();
        }

        [Fact]
        public void Property()
        {
            var filter = new Mock<IContextFilter>(MockBehavior.Strict);
            var condition = new Mock<IContextFilterCondition>(MockBehavior.Strict);

            filter.Setup(f => f.Add(It.Is<LogOp>(v => v == LogOp.And)))
                .Returns(condition.Object)
                .Verifiable();

            condition.Setup(f => f.Property(It.Is<string>(v => v == "property")))
                .Returns(condition.Object)
                .Verifiable();

            var condition1 = filter.Object.Property("property");
            
            condition1.Should().BeSameAs(condition.Object);

            condition.Verify();
            filter.Verify();
        }

        [Theory]
        [InlineData(CmpOp.Eq)]
        [InlineData(CmpOp.Exists)]
        [InlineData(CmpOp.IsNull)]
        public void Is_ToFilter(CmpOp op)
        {
            var filter = new Mock<IContextFilter>(MockBehavior.Strict);
            var condition = new Mock<IContextFilterCondition>(MockBehavior.Strict);

            filter.Setup(f => f.Add(It.Is<LogOp>(v => v == LogOp.And)))
                .Returns(condition.Object)
                .Verifiable();

            condition.Setup(f => f.Is(It.Is<CmpOp>(v => v == op)))
                .Returns(condition.Object)
                .Verifiable();

            var condition1 = filter.Object.Is(op);

            condition1.Should().BeSameAs(condition.Object);

            condition.Verify();
            filter.Verify();
        }

        [Fact]
        public void IsNull()
        {
            var filter = new Mock<IContextFilter>(MockBehavior.Strict);
            var condition = new Mock<IContextFilterCondition>(MockBehavior.Strict);

            filter.Setup(f => f.Add(It.Is<LogOp>(v => v == LogOp.And)))
                .Returns(condition.Object)
                .Verifiable();

            var sq = new MockSequence();

            condition.InSequence(sq).Setup(f => f.Is(It.Is<CmpOp>(v => v == CmpOp.IsNull)))
                .Returns(condition.Object)
                .Verifiable();

            condition.InSequence(sq).Setup(f => f.Property(It.Is<string>(v => v == "property")))
               .Returns(condition.Object)
               .Verifiable();

            var condition1 = filter.Object.IsNull("property");

            condition1.Should().BeSameAs(condition.Object);

            condition.Verify();
            filter.Verify();
        }

        [Fact]
        public void NotNull()
        {
            var filter = new Mock<IContextFilter>(MockBehavior.Strict);
            var condition = new Mock<IContextFilterCondition>(MockBehavior.Strict);

            filter.Setup(f => f.Add(It.Is<LogOp>(v => v == LogOp.And)))
                .Returns(condition.Object)
                .Verifiable();

            var sq = new MockSequence();

            condition.InSequence(sq).Setup(f => f.Is(It.Is<CmpOp>(v => v == CmpOp.NotNull)))
                .Returns(condition.Object)
                .Verifiable();

            condition.InSequence(sq).Setup(f => f.Property(It.Is<string>(v => v == "property")))
               .Returns(condition.Object)
               .Verifiable();

            var condition1 = filter.Object.NotNull("property");

            condition1.Should().BeSameAs(condition.Object);

            condition.Verify();
            filter.Verify();
        }

        [Theory]
        [InlineData("Eq", CmpOp.Eq)]
        [InlineData("Neq", CmpOp.Neq)]
        [InlineData("Ls", CmpOp.Ls)]
        [InlineData("Le", CmpOp.Le)]
        [InlineData("Gt", CmpOp.Gt)]
        [InlineData("Ge", CmpOp.Ge)]
        [InlineData("Like", CmpOp.Like)]
        [InlineData("IsNull", CmpOp.IsNull)]
        [InlineData("NotNull", CmpOp.NotNull)]
        public void Is_ToCondition(string name, CmpOp op)
        {
            var condition = new Mock<IContextFilterCondition>(MockBehavior.Strict);
            condition.Setup(c => c.Is(It.Is<CmpOp>(v => v == op)))
                .Returns(condition.Object)
                .Verifiable();

            var method = typeof(EntityFilterBuilderExtension).GetMethod(name, new Type[] { typeof(IContextFilterCondition) });
            method.Should().NotBeNull()
                .And.Subject.IsStatic.Should().BeTrue();

            method.Invoke(null, new object[] { condition.Object });

            condition.Verify();
        }
    }
}
