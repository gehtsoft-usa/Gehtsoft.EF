using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Test.Northwind;
using Gehtsoft.EF.Test.Utils;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Moq;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb.Query
{
    [Collection(nameof(NorthwindFixture))]
    public class ResilencyPolicy
    {
        private readonly NorthwindFixture mNorthwind;

        public ResilencyPolicy(NorthwindFixture northwind)
        {
            mNorthwind = northwind;

            ResiliencyPolicyDictionary.Instance.SetPolicy(
                "dummyConnectionString",
                null, true);

            ResiliencyPolicyDictionary.Instance.SetPolicy(
                "wrongConnectionString",
                null, true);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ExecuteNoData_Sync(bool setPolicy)
        {
            bool policyInvoked = false;

            var policy = new Mock<IResiliencyPolicy>();
            policy.Setup(s => s.Execute<int>(It.IsAny<Func<int>>()))
                .Callback<Func<int>>(s =>
                {
                    policyInvoked = true;
                    s.Invoke();
                })
                .Returns(123);
                
            ResiliencyPolicyDictionary.Instance.SetPolicy(
                setPolicy ? "dummyConnectionString" : "wrongConnectionString", 
                policy.Object, true);

            var dbconnection = new DummyDbConnection() { ConnectionString = "dummyConnectionString" };
            var efconnection = new DummySqlConnection(dbconnection);
            var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyQuery;
            dbquery.ExecuteNonQueryReturnValue = 456;

            query.ExecuteNoData().Should().Be(setPolicy ? 123 : 456);

            dbquery.ExecuteNonQueryCalled.Should().BeTrue();
            policyInvoked.Should().Be(setPolicy);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ExecuteNoData_Async(bool setPolicy)
        {
            bool policyInvoked = false;

            var policy = new Mock<IResiliencyPolicy>(MockBehavior.Strict);
            policy.Setup(s => s.ExecuteAsync<int>(It.IsAny<Func<CancellationToken, Task<int>>>(), It.IsAny<CancellationToken>()))
                .Callback<Func<CancellationToken, Task<int>>, CancellationToken>((action, token) =>
                {
                    policyInvoked = true;
                    action.Invoke(token);
                })
                .Returns(Task.FromResult(123));

            ResiliencyPolicyDictionary.Instance.SetPolicy(
                setPolicy ? "dummyConnectionString" : "wrongConnectionString",
                policy.Object, true);

            var dbconnection = new DummyDbConnection() { ConnectionString = "dummyConnectionString" };
            var efconnection = new DummySqlConnection(dbconnection);
            var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyQuery;
            dbquery.ExecuteNonQueryReturnValue = 456;

            (await query.ExecuteNoDataAsync()).Should().Be(setPolicy ? 123 : 456);

            dbquery.ExecuteNonQueryAsyncCalled.Should().BeTrue();
            policyInvoked.Should().Be(setPolicy);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ExecuteReaderData_Sync(bool setPolicy)
        {
            bool policyInvoked = false;

            var reader1 = new DummyDbDataReader();
            var reader2 = new DummyDbDataReader();

            var policy = new Mock<IResiliencyPolicy>(MockBehavior.Strict);
            policy.Setup(s => s.Execute<DbDataReader>(It.IsAny<Func<DbDataReader>>()))
                .Callback<Func<DbDataReader>>(s =>
                {
                    policyInvoked = true;
                    s.Invoke();
                })
                .Returns(reader1);

            ResiliencyPolicyDictionary.Instance.SetPolicy(
                setPolicy ? "dummyConnectionString" : "wrongConnectionString",
                policy.Object, true);

            var dbconnection = new DummyDbConnection() { ConnectionString = "dummyConnectionString" };
            var efconnection = new DummySqlConnection(dbconnection);
            var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyQuery;
            dbquery.ReturnReader = reader2;

            query.ExecuteReader();

            dbquery.ExecuteDbReaderCalled.Should().BeTrue();
            policyInvoked.Should().Be(setPolicy);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ExecuteReaderData_Async(bool setPolicy)
        {
            bool policyInvoked = false;

            var reader1 = new DummyDbDataReader();
            var reader2 = new DummyDbDataReader();

            var policy = new Mock<IResiliencyPolicy>(MockBehavior.Strict);
            policy.Setup(s => s.ExecuteAsync<DbDataReader>(It.IsAny<Func<CancellationToken, Task<DbDataReader>>>(), It.IsAny<CancellationToken>()))
                .Callback<Func<CancellationToken, Task<DbDataReader>>, CancellationToken>((s, t) =>
                {
                    policyInvoked = true;
                    s.Invoke(t);
                })
                .Returns(Task.FromResult(reader1 as DbDataReader));

            ResiliencyPolicyDictionary.Instance.SetPolicy(
                setPolicy ? "dummyConnectionString" : "wrongConnectionString",
                policy.Object, true);

            var dbconnection = new DummyDbConnection() { ConnectionString = "dummyConnectionString" };
            var efconnection = new DummySqlConnection(dbconnection);
            var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyQuery;
            dbquery.ReturnReader = reader2;

            await query.ExecuteReaderAsync();

            dbquery.ExecuteDbReaderAsyncCalled.Should().BeTrue();
            policyInvoked.Should().Be(setPolicy);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Read_Sync(bool setPolicy)
        {
            bool policyInvoked = false;

            var reader = new DummyDbDataReader();

            var policy = new Mock<IResiliencyPolicy>(MockBehavior.Strict);
            policy.Setup(s => s.Execute<DbDataReader>(It.IsAny<Func<DbDataReader>>()))
                .Callback<Func<DbDataReader>>(s =>
                {
                    policyInvoked = true;
                    s.Invoke();
                })
                .Returns(reader);

            policy.Setup(s => s.Execute<bool>(It.IsAny<Func<bool>>()))
                .Callback<Func<bool>>(s =>
                {
                    policyInvoked = true;
                    s.Invoke();
                })
                .Returns(false);

            ResiliencyPolicyDictionary.Instance.SetPolicy(
                setPolicy ? "dummyConnectionString" : "wrongConnectionString",
                policy.Object, true);

            var dbconnection = new DummyDbConnection() { ConnectionString = "dummyConnectionString" };
            var efconnection = new DummySqlConnection(dbconnection);
            var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyQuery;
            dbquery.ReturnReader = reader;

            query.ExecuteReader();

            query.ReadNext();

            reader.ReadCalled.Should().BeTrue();
            policyInvoked.Should().Be(setPolicy);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Read_Async(bool setPolicy)
        {
            bool policyInvoked = false;

            var reader = new DummyDbDataReader();

            var policy = new Mock<IResiliencyPolicy>(MockBehavior.Strict);
            policy.Setup(s => s.ExecuteAsync<DbDataReader>(It.IsAny<Func<CancellationToken, Task<DbDataReader>>>(), It.IsAny<CancellationToken>()))
                .Callback<Func<CancellationToken, Task<DbDataReader>>, CancellationToken>((s, t) =>
                {
                    policyInvoked = true;
                    s.Invoke(t);
                })
                .Returns(Task.FromResult(reader as DbDataReader));

            policy.Setup(s => s.ExecuteAsync<bool>(It.IsAny<Func<CancellationToken, Task<bool>>>(), It.IsAny<CancellationToken>()))
               .Callback<Func<CancellationToken, Task<bool>>, CancellationToken>((s, t) =>
               {
                   policyInvoked = true;
                   s.Invoke(t);
               })
               .Returns(Task.FromResult(false));

            ResiliencyPolicyDictionary.Instance.SetPolicy(
                setPolicy ? "dummyConnectionString" : "wrongConnectionString",
                policy.Object, true);

            var dbconnection = new DummyDbConnection() { ConnectionString = "dummyConnectionString" };
            var efconnection = new DummySqlConnection(dbconnection);
            var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyQuery;
            dbquery.ReturnReader = reader;

            await query.ExecuteReaderAsync();
            await query.ReadNextAsync();

            reader.ReadAsyncCalled.Should().BeTrue();
            policyInvoked.Should().Be(setPolicy);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void NextResult_Sync(bool setPolicy)
        {
            bool policyInvoked = false;

            var reader = new DummyDbDataReader();

            var policy = new Mock<IResiliencyPolicy>(MockBehavior.Strict);
            policy.Setup(s => s.Execute<DbDataReader>(It.IsAny<Func<DbDataReader>>()))
                .Callback<Func<DbDataReader>>(s =>
                {
                    policyInvoked = true;
                    s.Invoke();
                })
                .Returns(reader);

            policy.Setup(s => s.Execute<bool>(It.IsAny<Func<bool>>()))
                .Callback<Func<bool>>(s =>
                {
                    policyInvoked = true;
                    s.Invoke();
                })
                .Returns(false);

            ResiliencyPolicyDictionary.Instance.SetPolicy(
                setPolicy ? "dummyConnectionString" : "wrongConnectionString",
                policy.Object, true);

            var dbconnection = new DummyDbConnection() { ConnectionString = "dummyConnectionString" };
            var efconnection = new DummySqlConnection(dbconnection);
            var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyQuery;
            dbquery.ReturnReader = reader;

            query.ExecuteReader();

            query.NextReaderResult();

            reader.NextResultCalled.Should().BeTrue();
            policyInvoked.Should().Be(setPolicy);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task NextResult_Async(bool setPolicy)
        {
            bool policyInvoked = false;

            var reader = new DummyDbDataReader();

            var policy = new Mock<IResiliencyPolicy>(MockBehavior.Strict);
            policy.Setup(s => s.ExecuteAsync<DbDataReader>(It.IsAny<Func<CancellationToken, Task<DbDataReader>>>(), It.IsAny<CancellationToken>()))
                .Callback<Func<CancellationToken, Task<DbDataReader>>, CancellationToken>((s, t) =>
                {
                    policyInvoked = true;
                    s.Invoke(t);
                })
                .Returns(Task.FromResult(reader as DbDataReader));

            policy.Setup(s => s.ExecuteAsync<bool>(It.IsAny<Func<CancellationToken, Task<bool>>>(), It.IsAny<CancellationToken>()))
               .Callback<Func<CancellationToken, Task<bool>>, CancellationToken>((s, t) =>
               {
                   policyInvoked = true;
                   s.Invoke(t);
               })
               .Returns(Task.FromResult(false));

            ResiliencyPolicyDictionary.Instance.SetPolicy(
                setPolicy ? "dummyConnectionString" : "wrongConnectionString",
                policy.Object, true);

            var dbconnection = new DummyDbConnection() { ConnectionString = "dummyConnectionString" };
            var efconnection = new DummySqlConnection(dbconnection);
            var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyQuery;
            dbquery.ReturnReader = reader;

            await query.ExecuteReaderAsync();
            await query.NextReaderResultAsync();

            reader.NextResultAsyncCalled.Should().BeTrue();
            policyInvoked.Should().Be(setPolicy);
        }

        public class MyPolicy : IResiliencyPolicy
        {
            public int Count { get; private set; } = 0;

            public void Execute(Action action)
            {
                Count++;
                action();
            }

            public TResult Execute<TResult>(Func<TResult> action)
            {
                Count++;
                return action();
            }

            public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken token)
            {
                Count++;
                return action(token);
            }

            public Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken token)
            {
                Count++;
                return action(token);
            }
        }

        [Fact]
        public void PlayPolicyOnRealDb()
        {
            var connection = mNorthwind.GetInstance("sqlite-memory");
            var policy = new MyPolicy();

            using var removePolicy = new DelayedAction(() => ResiliencyPolicyDictionary.Instance.SetPolicy(connection.Connection.ConnectionString, null, true));
            ResiliencyPolicyDictionary.Instance.SetPolicy(connection.Connection.ConnectionString, policy, true);

            using (var query = connection.GetQuery("select * from nw_cust; select * from nw_empl;"))
            {
                query.ExecuteReader();

                query.Field(0).Name.Should().Be("customerID");
                query.NextReaderResult().Should().BeTrue();
                query.Field(0).Name.Should().Be("employeeID");
                query.NextReaderResult().Should().BeFalse();
            }

            policy.Count.Should().BeGreaterThan(0);
        }
    }
}
