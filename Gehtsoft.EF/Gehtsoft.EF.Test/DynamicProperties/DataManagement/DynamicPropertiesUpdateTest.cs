using System;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.DataManagement
{
    /// <summary>
    /// Single-entity Update of a dynamic-property owner, on every configured driver. Verifies the net
    /// changes are applied precisely: added properties inserted, changed ones updated, removed ones
    /// deleted, while untouched properties AND other owners' properties stay exactly as they were.
    ///
    /// After Insert the bag is already a non-new baseline (AcceptChanges), so we can modify it and
    /// Update without the not-yet-built load path.
    /// </summary>
    public class DynamicPropertiesUpdateTest : IClassFixture<SqlConnectionFixtureBase>
    {
        private readonly SqlConnectionFixtureBase mFixture;

        public static TheoryData<string> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);

        public DynamicPropertiesUpdateTest(SqlConnectionFixtureBase fixture)
        {
            mFixture = fixture;
        }

        [Entity(Scope = "dp_upd", Table = "dp_upd_owner")]
        [DynamicProperties]
        public class Owner : IDynamicPropertiesOwner
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }

            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        private static Owner InsertWithProps(SqlDbConnection connection, string name, Action<DynamicPropertyBag> fill)
        {
            var e = new Owner { Name = name };
            var bag = e.InitializeDynamicProperties();
            fill?.Invoke(bag);
            using (var q = connection.GetInsertEntityQuery<Owner>())
                q.Execute(e);
            return e; // e.DynamicProperties is now a non-new baseline of the inserted values
        }

        private static void Recreate(SqlDbConnection c)
        {
            using (var q = c.GetDropEntityQuery<Owner>()) q.Execute();
            using (var q = c.GetCreateEntityQuery<Owner>()) q.Execute();
        }

        private static void Drop(SqlDbConnection c)
        {
            using (var q = c.GetDropEntityQuery<Owner>()) q.Execute();
        }

        private static int PropCount(SqlDbConnection c, int owner) => Scalar<int>(c, $"SELECT COUNT(*) FROM dp_upd_owner_props WHERE owner = {owner}");
        private static bool HasProp(SqlDbConnection c, int owner, string name) => Scalar<int>(c, $"SELECT COUNT(*) FROM dp_upd_owner_props WHERE owner = {owner} AND name = '{name}'") > 0;
        private static long IntProp(SqlDbConnection c, int owner, string name) => Column<long>(c, "v_int", owner, name);
        private static double RealProp(SqlDbConnection c, int owner, string name) => Column<double>(c, "v_real", owner, name);
        private static string StrProp(SqlDbConnection c, int owner, string name) => Column<string>(c, "v_str", owner, name);

        private static T Scalar<T>(SqlDbConnection c, string sql)
        {
            using (var q = c.GetQuery(sql, true))
            {
                q.ExecuteReader();
                q.ReadNext();
                return q.GetValue<T>(0);
            }
        }

        private static T Column<T>(SqlDbConnection c, string column, int owner, string name)
        {
            using (var q = c.GetQuery($"SELECT {column} FROM dp_upd_owner_props WHERE owner = {owner} AND name = '{name}'", true))
            {
                q.ExecuteReader();
                q.ReadNext();
                return q.GetValue<T>(0);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_AddsNewProperty(string connectionName)
        {
            var c = mFixture.GetInstance(connectionName);
            Recreate(c);
            try
            {
                var e = InsertWithProps(c, "e", b => b.Set("a", 1));
                e.DynamicProperties.Set("b", "hello");
                using (var q = c.GetUpdateEntityQuery<Owner>()) q.Execute(e);

                PropCount(c, e.Id).Should().Be(2);
                IntProp(c, e.Id, "a").Should().Be(1);       // unchanged
                StrProp(c, e.Id, "b").Should().Be("hello"); // added
            }
            finally { Drop(c); }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_ChangesExistingProperty(string connectionName)
        {
            var c = mFixture.GetInstance(connectionName);
            Recreate(c);
            try
            {
                var e = InsertWithProps(c, "e", b => { b.Set("a", 1); b.Set("b", "x"); });
                e.DynamicProperties.Set("a", 42);
                using (var q = c.GetUpdateEntityQuery<Owner>()) q.Execute(e);

                PropCount(c, e.Id).Should().Be(2);
                IntProp(c, e.Id, "a").Should().Be(42);   // changed
                StrProp(c, e.Id, "b").Should().Be("x");  // unchanged
            }
            finally { Drop(c); }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_RemovesProperty(string connectionName)
        {
            var c = mFixture.GetInstance(connectionName);
            Recreate(c);
            try
            {
                var e = InsertWithProps(c, "e", b => { b.Set("a", 1); b.Set("b", "x"); });
                e.DynamicProperties.Remove("b");
                using (var q = c.GetUpdateEntityQuery<Owner>()) q.Execute(e);

                PropCount(c, e.Id).Should().Be(1);
                IntProp(c, e.Id, "a").Should().Be(1);        // unchanged
                HasProp(c, e.Id, "b").Should().BeFalse();    // removed
            }
            finally { Drop(c); }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_Mixed_ChangesOnlyWhatChanged_OthersUntouched(string connectionName)
        {
            var c = mFixture.GetInstance(connectionName);
            Recreate(c);
            try
            {
                var e = InsertWithProps(c, "e", b => { b.Set("a", 1); b.Set("b", "x"); b.Set("c", 1.5); });
                var other = InsertWithProps(c, "other", b => { b.Set("a", 99); b.Set("z", "zzz"); });

                // add d, change a, remove b, leave c untouched
                e.DynamicProperties.Set("d", 7);
                e.DynamicProperties.Set("a", 2);
                e.DynamicProperties.Remove("b");
                using (var q = c.GetUpdateEntityQuery<Owner>()) q.Execute(e);

                // e: a changed, c unchanged, d added, b removed
                PropCount(c, e.Id).Should().Be(3);
                IntProp(c, e.Id, "a").Should().Be(2);
                RealProp(c, e.Id, "c").Should().Be(1.5);
                IntProp(c, e.Id, "d").Should().Be(7);
                HasProp(c, e.Id, "b").Should().BeFalse();

                // the other owner's properties are completely untouched
                PropCount(c, other.Id).Should().Be(2);
                IntProp(c, other.Id, "a").Should().Be(99);
                StrProp(c, other.Id, "z").Should().Be("zzz");
            }
            finally { Drop(c); }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public async System.Threading.Tasks.Task Update_Mixed_Async(string connectionName)
        {
            var c = mFixture.GetInstance(connectionName);
            Recreate(c);
            try
            {
                var e = InsertWithProps(c, "e", b => { b.Set("a", 1); b.Set("b", "x"); });
                e.DynamicProperties.Set("a", 2);
                e.DynamicProperties.Set("c", "new");
                e.DynamicProperties.Remove("b");
                using (var q = c.GetUpdateEntityQuery<Owner>()) await q.ExecuteAsync(e);

                PropCount(c, e.Id).Should().Be(2);
                IntProp(c, e.Id, "a").Should().Be(2);
                StrProp(c, e.Id, "c").Should().Be("new");
                HasProp(c, e.Id, "b").Should().BeFalse();
            }
            finally { Drop(c); }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_NewBag_IsRejected(string connectionName)
        {
            var c = mFixture.GetInstance(connectionName);
            Recreate(c);
            try
            {
                var e = InsertWithProps(c, "e", b => b.Set("a", 1));
                // attach a fresh (new) bag - as if the caller mistakenly re-initialized instead of loading
                e.InitializeDynamicProperties();
                e.DynamicProperties.Set("a", 2);

                Action act = () =>
                {
                    using (var q = c.GetUpdateEntityQuery<Owner>()) q.Execute(e);
                };
                act.Should().Throw<EfSqlException>();

                // the stored value is unchanged (rejected)
                IntProp(c, e.Id, "a").Should().Be(1);
            }
            finally { Drop(c); }
        }
    }
}
