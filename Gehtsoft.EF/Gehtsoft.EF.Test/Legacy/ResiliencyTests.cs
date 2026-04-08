using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Xunit;

namespace Gehtsoft.EF.Test.Legacy
{
    public class ResiliencyTests
    {
        private class ResilencyPolicy : IResiliencyPolicy
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

            public Task ExecuteAsync(Func<Task> action)
            {
                Count++;
                return action();
            }

            public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken token)
            {
                Count++;
                return action(token);
            }

            public Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> action)
            {
                Count++;
                return action();
            }

            public Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken token)
            {
                Count++;
                return action(token);
            }
        }

        [Fact]
        public void TestDictionary()
        {
            ResiliencyPolicyDictionary.Instance.Should().NotBeNull();
            ResiliencyPolicyDictionary.Instance.GetPolicy("mystring").Should().BeNull();

            ResilencyPolicy policy1 = new ResilencyPolicy();
            ResilencyPolicy policy2 = new ResilencyPolicy();
            ResilencyPolicy policy3 = new ResilencyPolicy();
            ResilencyPolicy policy4 = new ResilencyPolicy();

            Action<IResiliencyPolicy, bool> setGlobalPolicy = (policy, force) => ResiliencyPolicyDictionary.Instance.SetGlobalPolicy(policy, force);

            setGlobalPolicy.Invoking(action => action(policy1, false)).Should().NotThrow();

            ResiliencyPolicyDictionary.Instance.GetPolicy("mystring")
                .Should().NotBeNull()
                .And.Be(policy1);

            setGlobalPolicy.Invoking(action => action(policy2, false)).Should().Throw<InvalidOperationException>();
            ResiliencyPolicyDictionary.Instance.GetPolicy("mystring").Should().Be(policy1);
            setGlobalPolicy.Invoking(action => action(policy2, true)).Should().NotThrow<InvalidOperationException>();
            ResiliencyPolicyDictionary.Instance.GetPolicy("mystring")
                .Should().NotBeNull()
                .And.Be(policy2);

            Action<string, IResiliencyPolicy, bool> setPolicy = (connectionString, policy, force) => ResiliencyPolicyDictionary.Instance.SetPolicy(connectionString, policy, force);

            setPolicy.Invoking(action => action("mystring", policy3, false)).Should().NotThrow();
            ResiliencyPolicyDictionary.Instance.GetPolicy("mystring")
                .Should().NotBeNull()
                .And.Be(policy3);

            setPolicy.Invoking(action => action("mystring", policy4, false)).Should().Throw<InvalidOperationException>();
            ResiliencyPolicyDictionary.Instance.GetPolicy("mystring").Should().Be(policy3);

            setPolicy.Invoking(action => action("mystring", policy4, true)).Should().NotThrow<InvalidOperationException>();
            ResiliencyPolicyDictionary.Instance.GetPolicy("mystring").Should().Be(policy4);
            ResiliencyPolicyDictionary.Instance.GetPolicy("mystring1").Should().Be(policy2);
        }

        [Fact]
        public async Task TestInterface()
        {
            ResilencyPolicy policy = new ResilencyPolicy();
            ResiliencyPolicyDictionary.Instance.SetPolicy("Data Source=:memory:", policy);
            policy.Count.Should().Be(0);
            using (var connection = UniversalSqlDbFactory.Create(UniversalSqlDbFactory.SQLITE, "Data Source=:memory:"))
            {
                policy.Count.Should().Be(1);
            }

            using (var connection = await UniversalSqlDbFactory.CreateAsync(UniversalSqlDbFactory.SQLITE, "Data Source=:memory:"))
            {
                policy.Count.Should().Be(2);

                using (var query = connection.GetQuery("create table testres(field string)"))
                {
                    query.ExecuteNoData();
                    policy.Count.Should().Be(3);
                }

                using (var query = connection.GetQuery("select * from testres; select * from testres;"))
                {
                    query.ExecuteReader();
                    policy.Count.Should().Be(4);
                    query.ReadNext();
                    policy.Count.Should().Be(5);
                    query.NextReaderResult();
                    policy.Count.Should().Be(6);
                    query.ReadNext();
                    policy.Count.Should().Be(7);
                }

                using (var query = connection.GetQuery("select * from testres; select * from testres;"))
                {
                    await query.ExecuteReaderAsync();
                    policy.Count.Should().Be(8);
                    (await query.ReadNextAsync()).Should().Be(false);
                    policy.Count.Should().Be(9);
                    (await query.NextReaderResultAsync()).Should().Be(true);
                    policy.Count.Should().Be(10);
                    (await query.ReadNextAsync()).Should().Be(false);
                    policy.Count.Should().Be(11);
                }

                using (var query = connection.GetQuery("drop table testres"))
                {
                    await query.ExecuteNoDataAsync();
                    policy.Count.Should().Be(12);
                }
            }
        }
    }
}
