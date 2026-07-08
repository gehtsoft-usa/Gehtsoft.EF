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
    /// MultiUpdate for a dynamic-property owner, on every driver. Covers the three cases: owner-column
    /// only (no props touched), dynamic-properties only (bulk set/remove), and mixed. Uses a
    /// dynamic-property condition where relevant (the self-referencing case that forces the
    /// materialize-ids path). Verifies matched rows change, unchanged properties and other owners stay.
    /// </summary>
    public class DynamicPropertiesMultiUpdateTest : IClassFixture<SqlConnectionFixtureBase>
    {
        private readonly SqlConnectionFixtureBase mFixture;
        public static TheoryData<string> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);
        public DynamicPropertiesMultiUpdateTest(SqlConnectionFixtureBase fixture) { mFixture = fixture; }

        [Entity(Scope = "dp_mupd", Table = "dp_mupd_owner")]
        [DynamicProperties]
        public class Owner : IDynamicPropertiesOwner
        {
            [AutoId] public int Id { get; set; }
            [EntityProperty(Size = 32, Nullable = true)] public string Name { get; set; }
            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        private static Owner Insert(SqlDbConnection c, string name, Action<DynamicPropertyBag> fill)
        {
            var e = new Owner { Name = name };
            var bag = e.InitializeDynamicProperties();
            fill?.Invoke(bag);
            using (var q = c.GetInsertEntityQuery<Owner>()) q.Execute(e);
            return e;
        }

        private static string NameOf(SqlDbConnection c, int id)
        {
            using (var q = c.GetSelectEntitiesQuery<Owner>())
            {
                q.Where.Property(nameof(Owner.Id)).Eq(id);
                return q.ReadOne<Owner>().Name;
            }
        }

        private static int AllProps(SqlDbConnection c) => Scalar(c, "SELECT COUNT(*) FROM dp_mupd_owner_props");
        private static int PropCount(SqlDbConnection c, int owner) => Scalar(c, $"SELECT COUNT(*) FROM dp_mupd_owner_props WHERE owner = {owner}");
        private static bool HasProp(SqlDbConnection c, int owner, string name) => Scalar(c, $"SELECT COUNT(*) FROM dp_mupd_owner_props WHERE owner = {owner} AND name = '{name}'") > 0;

        private static int Scalar(SqlDbConnection c, string sql)
        {
            using (var q = c.GetQuery(sql, true)) { q.ExecuteReader(); q.ReadNext(); return q.GetValue<int>(0); }
        }

        private static string StrProp(SqlDbConnection c, int owner, string name)
        {
            using (var q = c.GetQuery($"SELECT v_str FROM dp_mupd_owner_props WHERE owner = {owner} AND name = '{name}'", true))
            { q.ExecuteReader(); q.ReadNext(); return q.GetValue<string>(0); }
        }

        private static long IntProp(SqlDbConnection c, int owner, string name)
        {
            using (var q = c.GetQuery($"SELECT v_int FROM dp_mupd_owner_props WHERE owner = {owner} AND name = '{name}'", true))
            { q.ExecuteReader(); q.ReadNext(); return q.GetValue<long>(0); }
        }

        private static void Recreate(SqlDbConnection c)
        {
            using (var q = c.GetDropEntityQuery<Owner>()) q.Execute();
            using (var q = c.GetCreateEntityQuery<Owner>()) q.Execute();
        }
        private static void Drop(SqlDbConnection c) { using (var q = c.GetDropEntityQuery<Owner>()) q.Execute(); }

        // three "red/blue" owners; returns them
        private static (Owner red1, Owner red2, Owner blue1) Seed(SqlDbConnection c)
        {
            var red1 = Insert(c, "red1", b => { b.Set("color", "red"); b.Set("size", 10); });
            var red2 = Insert(c, "red2", b => { b.Set("color", "red"); b.Set("size", 20); });
            var blue1 = Insert(c, "blue1", b => { b.Set("color", "blue"); b.Set("size", 30); });
            return (red1, red2, blue1); // 6 property rows
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void OwnerColumnOnly_ByDynamicCondition_PropsUntouched(string connectionName)
        {
            var c = mFixture.GetInstance(connectionName);
            Recreate(c);
            try
            {
                var (red1, red2, blue1) = Seed(c);

                using (var q = c.GetMultiUpdateEntityQuery<Owner>())
                {
                    q.AddUpdateColumn(nameof(Owner.Name), "UPD");
                    q.Where.DynamicPropertyOf<Owner>("color").Eq("red");
                    q.Execute();
                }

                NameOf(c, red1.Id).Should().Be("UPD");
                NameOf(c, red2.Id).Should().Be("UPD");
                NameOf(c, blue1.Id).Should().Be("blue1"); // not matched
                AllProps(c).Should().Be(6);                // no property rows touched
            }
            finally { Drop(c); }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void PropertiesOnly_Set_ByDynamicCondition(string connectionName)
        {
            var c = mFixture.GetInstance(connectionName);
            Recreate(c);
            try
            {
                var (red1, red2, blue1) = Seed(c);

                using (var q = c.GetMultiUpdateEntityQuery<Owner>())
                {
                    q.SetDynamicProperty("status", "on");
                    q.Where.DynamicPropertyOf<Owner>("color").Eq("red");
                    q.Execute();
                }

                // matched reds gain 'status', keep their other props; owner column unchanged
                StrProp(c, red1.Id, "status").Should().Be("on");
                StrProp(c, red2.Id, "status").Should().Be("on");
                IntProp(c, red1.Id, "size").Should().Be(10);   // unchanged
                PropCount(c, red1.Id).Should().Be(3);
                NameOf(c, red1.Id).Should().Be("red1");         // owner column not touched

                // blue untouched
                HasProp(c, blue1.Id, "status").Should().BeFalse();
                PropCount(c, blue1.Id).Should().Be(2);
            }
            finally { Drop(c); }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void PropertiesOnly_Set_Replaces(string connectionName)
        {
            var c = mFixture.GetInstance(connectionName);
            Recreate(c);
            try
            {
                var red1 = Insert(c, "red1", b => { b.Set("color", "red"); b.Set("status", "old"); });

                using (var q = c.GetMultiUpdateEntityQuery<Owner>())
                {
                    q.SetDynamicProperty("status", "new");
                    q.Where.DynamicPropertyOf<Owner>("color").Eq("red");
                    q.Execute();
                }

                StrProp(c, red1.Id, "status").Should().Be("new"); // replaced, not duplicated
                PropCount(c, red1.Id).Should().Be(2);             // color + status
            }
            finally { Drop(c); }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void PropertiesOnly_Remove_ByDynamicCondition(string connectionName)
        {
            var c = mFixture.GetInstance(connectionName);
            Recreate(c);
            try
            {
                var (red1, red2, blue1) = Seed(c);

                using (var q = c.GetMultiUpdateEntityQuery<Owner>())
                {
                    q.RemoveDynamicProperty("size");
                    q.Where.DynamicPropertyOf<Owner>("color").Eq("red");
                    q.Execute();
                }

                HasProp(c, red1.Id, "size").Should().BeFalse();
                HasProp(c, red2.Id, "size").Should().BeFalse();
                PropCount(c, red1.Id).Should().Be(1);           // color only
                IntProp(c, blue1.Id, "size").Should().Be(30);   // other owner untouched
                PropCount(c, blue1.Id).Should().Be(2);
            }
            finally { Drop(c); }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Mixed_OwnerColumnAndProperties_ByDynamicCondition(string connectionName)
        {
            var c = mFixture.GetInstance(connectionName);
            Recreate(c);
            try
            {
                var (red1, red2, blue1) = Seed(c);

                using (var q = c.GetMultiUpdateEntityQuery<Owner>())
                {
                    q.AddUpdateColumn(nameof(Owner.Name), "M");
                    q.SetDynamicProperty("status", "on");
                    q.RemoveDynamicProperty("size");
                    q.Where.DynamicPropertyOf<Owner>("color").Eq("red");
                    q.Execute();
                }

                // BOTH matched reds: Name changed, status added, size removed, color kept
                foreach (int id in new[] { red1.Id, red2.Id })
                {
                    NameOf(c, id).Should().Be("M");
                    StrProp(c, id, "status").Should().Be("on");
                    HasProp(c, id, "size").Should().BeFalse();
                    StrProp(c, id, "color").Should().Be("red");
                    PropCount(c, id).Should().Be(2); // color + status
                }

                // blue untouched
                NameOf(c, blue1.Id).Should().Be("blue1");
                PropCount(c, blue1.Id).Should().Be(2);
                HasProp(c, blue1.Id, "status").Should().BeFalse();
            }
            finally { Drop(c); }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void ManyOwners_CrossesBatchBoundary(string connectionName)
        {
            var c = mFixture.GetInstance(connectionName);
            Recreate(c);
            try
            {
                // 60 matched owners > the 50-row batch size, so the operation spans two batches
                const int matched = 60;
                for (int i = 0; i < matched; i++)
                    Insert(c, "m" + i, b => b.Set("color", "red"));
                Insert(c, "other", b => b.Set("color", "blue"));

                using (var q = c.GetMultiUpdateEntityQuery<Owner>())
                {
                    q.SetDynamicProperty("status", "on");
                    q.Where.DynamicPropertyOf<Owner>("color").Eq("red");
                    q.Execute();
                }

                // every matched owner (across both batches) got exactly one 'status' row; the blue one did not
                Scalar(c, "SELECT COUNT(*) FROM dp_mupd_owner_props WHERE name = 'status'").Should().Be(matched);
            }
            finally { Drop(c); }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public async System.Threading.Tasks.Task Mixed_Async(string connectionName)
        {
            var c = mFixture.GetInstance(connectionName);
            Recreate(c);
            try
            {
                var (red1, _, blue1) = Seed(c);

                using (var q = c.GetMultiUpdateEntityQuery<Owner>())
                {
                    q.AddUpdateColumn(nameof(Owner.Name), "M");
                    q.SetDynamicProperty("status", "on");
                    q.Where.DynamicPropertyOf<Owner>("color").Eq("red");
                    await q.ExecuteAsync();
                }

                NameOf(c, red1.Id).Should().Be("M");
                StrProp(c, red1.Id, "status").Should().Be("on");
                NameOf(c, blue1.Id).Should().Be("blue1");
                HasProp(c, blue1.Id, "status").Should().BeFalse();
            }
            finally { Drop(c); }
        }
    }
}
