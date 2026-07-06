using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.DataSelecting
{
    /// <summary>
    /// The load path: populating the dynamic-property bag on select. Two entry points, on every driver:
    ///  * <see cref="SelectEntitiesQuery.PreloadProperties"/> - ReadAll attaches a loaded bag to every
    ///    entity (batched after the rows are read);
    ///  * <see cref="EntityConnectionExtension.LoadPropertiesFor{T}"/> - load on demand for one entity.
    /// A loaded bag is not "new" and reports no modifications; an owner with no property rows gets an
    /// empty bag; a direct ReadOne with PreloadProperties throws.
    /// </summary>
    public class DynamicPropertiesLoadTest : IClassFixture<SqlConnectionFixtureBase>
    {
        private readonly SqlConnectionFixtureBase mFixture;
        public static TheoryData<string> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);
        public DynamicPropertiesLoadTest(SqlConnectionFixtureBase fixture) { mFixture = fixture; }

        [Entity(Scope = "dp_load", Table = "dp_load_owner")]
        [DynamicProperties]
        public class Owner : IDynamicPropertiesOwner
        {
            [AutoId] public int Id { get; set; }
            [EntityProperty(Size = 32, Nullable = true)] public string Name { get; set; }
            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        private static readonly DateTime SampleUtc = new DateTime(2021, 3, 4, 5, 6, 7, DateTimeKind.Utc);

        private static int Insert(SqlDbConnection c, string name, Action<DynamicPropertyBag> fill)
        {
            var e = new Owner { Name = name };
            var bag = e.InitializeDynamicProperties();
            fill?.Invoke(bag);
            using (var q = c.GetInsertEntityQuery<Owner>()) q.Execute(e);
            return e.Id;
        }

        private static void Seed(SqlDbConnection c)
        {
            Insert(c, "full", b =>
            {
                b.Set("color", "red");
                b.Set("size", 10);
                b.Set("big", 5_000_000_000L);
                b.Set("weight", 1.5);
                b.Set("active", true);
                b.Set("when", SampleUtc);
            });
            Insert(c, "one", b => b.Set("color", "blue"));
            Insert(c, "none", null);   // owner with no property rows
        }

        private void Run(string connectionName, Action<SqlDbConnection> body)
        {
            var c = mFixture.GetInstance(connectionName);
            using (var q = c.GetDropEntityQuery<Owner>()) q.Execute();
            using (var q = c.GetCreateEntityQuery<Owner>()) q.Execute();
            try { Seed(c); body(c); }
            finally { using (var q = c.GetDropEntityQuery<Owner>()) q.Execute(); }
        }

        private async Task RunAsync(string connectionName, Func<SqlDbConnection, Task> body)
        {
            var c = mFixture.GetInstance(connectionName);
            using (var q = c.GetDropEntityQuery<Owner>()) q.Execute();
            using (var q = c.GetCreateEntityQuery<Owner>()) q.Execute();
            try { Seed(c); await body(c); }
            finally { using (var q = c.GetDropEntityQuery<Owner>()) q.Execute(); }
        }

        private static Owner ByName(IEnumerable<Owner> list, string name)
        {
            foreach (var o in list)
                if (o.Name == name)
                    return o;
            return null;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Preload_ReadAll_AttachesLoadedBags(string connectionName)
            => Run(connectionName, c =>
            {
                List<Owner> all;
                using (var q = c.GetSelectEntitiesQuery<Owner>())
                {
                    q.PreloadProperties = true;
                    all = new List<Owner>(q.ReadAll<Owner>());
                }

                var full = ByName(all, "full");
                full.DynamicProperties.Should().NotBeNull();
                full.DynamicProperties.IsNew.Should().BeFalse();
                full.DynamicProperties.AnyModified.Should().BeFalse();

                full.DynamicProperties.Get<string>("color").Should().Be("red");
                full.DynamicProperties.Get<int>("size").Should().Be(10);
                full.DynamicProperties.Get<long>("big").Should().Be(5_000_000_000L);
                full.DynamicProperties.Get<double>("weight").Should().Be(1.5);
                full.DynamicProperties.Get<bool>("active").Should().BeTrue();
                full.DynamicProperties.Get<DateTime>("when").Should().Be(SampleUtc);
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Preload_ReadAll_OwnerWithoutProperties_GetsEmptyBag(string connectionName)
            => Run(connectionName, c =>
            {
                List<Owner> all;
                using (var q = c.GetSelectEntitiesQuery<Owner>())
                {
                    q.PreloadProperties = true;
                    all = new List<Owner>(q.ReadAll<Owner>());
                }

                var none = ByName(all, "none");
                none.DynamicProperties.Should().NotBeNull();
                none.DynamicProperties.IsNew.Should().BeFalse();
                none.DynamicProperties.Count.Should().Be(0);

                // an owner with exactly one property still isolates from the others
                var one = ByName(all, "one");
                one.DynamicProperties.Count.Should().Be(1);
                one.DynamicProperties.Contains("color").Should().BeTrue();
                one.DynamicProperties.Get<string>("color").Should().Be("blue");
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void LoadPropertiesFor_OnDemand_FillsBag(string connectionName)
            => Run(connectionName, c =>
            {
                Owner one;
                using (var q = c.GetSelectEntitiesQuery<Owner>())
                {
                    q.Where.Property(nameof(Owner.Name)).Eq("one");
                    one = q.ReadOne<Owner>();     // no preload -> bag not populated yet
                }

                c.LoadPropertiesFor(one);
                one.DynamicProperties.Should().NotBeNull();
                one.DynamicProperties.IsNew.Should().BeFalse();
                one.DynamicProperties.Get<string>("color").Should().Be("blue");
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public Task Preload_ReadAllAsync_AttachesLoadedBags(string connectionName)
            => RunAsync(connectionName, async c =>
            {
                List<Owner> all;
                using (var q = c.GetSelectEntitiesQuery<Owner>())
                {
                    q.PreloadProperties = true;
                    all = new List<Owner>(await q.ReadAllAsync<Owner>());
                }

                var full = ByName(all, "full");
                full.DynamicProperties.Should().NotBeNull();
                full.DynamicProperties.IsNew.Should().BeFalse();
                full.DynamicProperties.AnyModified.Should().BeFalse();
                full.DynamicProperties.Get<string>("color").Should().Be("red");
                full.DynamicProperties.Get<long>("big").Should().Be(5_000_000_000L);
                full.DynamicProperties.Get<DateTime>("when").Should().Be(SampleUtc);

                ByName(all, "none").DynamicProperties.Count.Should().Be(0);
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public Task LoadPropertiesForAsync_OnDemand_FillsBag(string connectionName)
            => RunAsync(connectionName, async c =>
            {
                Owner one;
                using (var q = c.GetSelectEntitiesQuery<Owner>())
                {
                    q.Where.Property(nameof(Owner.Name)).Eq("one");
                    one = q.ReadOne<Owner>();     // no preload -> bag not populated yet
                }

                await c.LoadPropertiesForAsync(one);
                one.DynamicProperties.Should().NotBeNull();
                one.DynamicProperties.IsNew.Should().BeFalse();
                one.DynamicProperties.Get<string>("color").Should().Be("blue");
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Preload_DirectReadOne_Throws(string connectionName)
            => Run(connectionName, c =>
            {
                using (var q = c.GetSelectEntitiesQuery<Owner>())
                {
                    q.PreloadProperties = true;
                    Action act = () => q.ReadOne<Owner>();
                    act.Should().Throw<NotSupportedException>();
                }
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Preload_LoadedBag_RoundTripsThroughUpdate(string connectionName)
            => Run(connectionName, c =>
            {
                // load, mutate one property, save, reload -> the change (and only it) persists
                Owner full;
                using (var q = c.GetSelectEntitiesQuery<Owner>())
                {
                    q.PreloadProperties = true;
                    q.Where.Property(nameof(Owner.Name)).Eq("full");
                    full = new List<Owner>(q.ReadAll<Owner>())[0];
                }

                full.DynamicProperties.Set("size", 99);
                using (var q = c.GetUpdateEntityQuery<Owner>()) q.Execute(full);

                Owner reloaded;
                using (var q = c.GetSelectEntitiesQuery<Owner>())
                {
                    q.PreloadProperties = true;
                    q.Where.Property(nameof(Owner.Name)).Eq("full");
                    reloaded = new List<Owner>(q.ReadAll<Owner>())[0];
                }

                reloaded.DynamicProperties.Get<int>("size").Should().Be(99);
                reloaded.DynamicProperties.Get<string>("color").Should().Be("red");   // untouched
            });
    }
}
