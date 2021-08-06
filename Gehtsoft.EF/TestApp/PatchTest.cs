using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.CreateEntity.Patch;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Moq;
using NUnit.Framework;

namespace TestApp
{
    public class PatchTest
    {
        private static Dictionary<Type, int> gInvocations = new Dictionary<Type, int>();

        private static void ClearInvokations() => gInvocations.Clear();

        private static void Invoke(object obj)
        {
            var t = obj.GetType();
            if (gInvocations.TryGetValue(t, out int x))
                gInvocations[t] = x + 1;
            else
                gInvocations[t] = 1;
        }

        private static int Count<T>()
        {
            if (gInvocations.TryGetValue(typeof(T), out int x))
                return x;
            return 0;
        }

        [EfPatch("patchtest1", 1, 1, 1)]
        public class Patch111 : IEfPatch
        {
            public void Apply(SqlDbConnection connection)
            {
                Invoke(this);
            }
        }

        [EfPatch("patchtest1", 1, 1, 2)]
        public class Patch112 : IEfPatch
        {
            public void Apply(SqlDbConnection connection)
            {
                Invoke(this);
            }
        }
        
        [EfPatch("patchtest1", 1, 2, 1)]
        public class Patch121 : IEfPatch
        {
            public void Apply(SqlDbConnection connection)
            {
                Invoke(this);
            }
        }

        [EfPatch("patchtest1", 1, 2, 2)]
        public class Patch122 : IEfPatch
        {
            public void Apply(SqlDbConnection connection)
            {
                Invoke(this);
            }
        }

        [EfPatch("patchtest1", 1, 2, 3)]
        public class Patch123 : IEfPatch
        {
            public void Apply(SqlDbConnection connection)
            {
                Invoke(this);
            }
        }

        [EfPatch("patchtest1", 2, 1, 1)]
        public class Patch211 : IEfPatch
        {
            public void Apply(SqlDbConnection connection)
            {
                Invoke(this);
            }
        }

        [EfPatch("patchtest1", 2, 2, 1)]
        public class Patch221 : IEfPatch
        {
            public void Apply(SqlDbConnection connection)
            {
                Invoke(this);
            }
        }

        [EfPatch("patchtest1", 3, 2, 1)]
        public class Patch321 : IEfPatch
        {
            public void Apply(SqlDbConnection connection)
            {
                Invoke(this);
            }
        }

        [TestCase]
        public void Find_Success()
        {
            var patches = EfPatchProcessor.FindAllPatches(new Assembly[] { this.GetType().Assembly }, "patchtest1");
            patches.Should().NotBeNullOrEmpty();
            patches.Should().HaveCount(8);
            patches[0].PatchType.Should().Be(typeof(Patch111));
            patches[1].PatchType.Should().Be(typeof(Patch112));
            patches[2].PatchType.Should().Be(typeof(Patch121));
            patches[3].PatchType.Should().Be(typeof(Patch122));
            patches[4].PatchType.Should().Be(typeof(Patch123));
            patches[5].PatchType.Should().Be(typeof(Patch211));
            patches[6].PatchType.Should().Be(typeof(Patch221));
            patches[7].PatchType.Should().Be(typeof(Patch321));
        }
        
        [TestCase]
        public void Find_None()
        {
            var patches = EfPatchProcessor.FindAllPatches(new Assembly[] { this.GetType().Assembly }, "nonexistent");
            patches.Should().NotBeNull();
            patches.Should().HaveCount(0);
        }

        [TestCase]
        public void NewPatchTable_PatchesExist()
        {
            ClearInvokations();

            var patches = EfPatchProcessor.FindAllPatches(new Assembly[] { this.GetType().Assembly }, "patchtest1");

            using var connection = SqliteDbConnectionFactory.CreateMemory();
            connection.ApplyPatches(patches, "patchtest1");

            connection.Schema().Should().Contain(t => t.Name == "ef_patch_history");
            
            using (var query = connection.GetSelectEntitiesQuery<EfPatchHistoryRecord>())
            {
                var c = query.ReadAll<EfPatchHistoryRecord>();
                c.Should().HaveCount(1);
                c[0].Scope.Should().Be("patchtest1");
                c[0].MajorVersion.Should().Be(3);
                c[0].MinorVersion.Should().Be(2);
                c[0].PatchVersion.Should().Be(1);
                c[0].Applied.Should().BeWithin(TimeSpan.FromSeconds(1));
            }

            gInvocations.Should().BeEmpty();
        }

        [TestCase]
        public void NewPatchTable_PatchesNotExist()
        {
            ClearInvokations();

            var patches = new List<EfPatchProcessor.EfPatchInstance>();

            using var connection = SqliteDbConnectionFactory.CreateMemory();
            connection.ApplyPatches(patches, "patchtest1");

            connection.Schema().Should().Contain(t => t.Name == "ef_patch_history");

            using (var query = connection.GetSelectEntitiesQuery<EfPatchHistoryRecord>())
            {
                var c = query.ReadAll<EfPatchHistoryRecord>();
                c.Should().HaveCount(0);
            }

            gInvocations.Should().BeEmpty();
        }

        private void Match<T>(EntityCollection<EfPatchHistoryRecord> collection, int index)
        {
            Count<T>().Should().Be(1);
            collection.Count.Should().BeGreaterThan(index);

            var r = collection[index];
            var attr = typeof(T).GetCustomAttribute<EfPatchAttribute>();

            attr.Should().NotBeNull();

            r.Scope.Should().Be(attr.Scope);
            r.MajorVersion.Should().Be(attr.MajorVersion);
            r.MinorVersion.Should().Be(attr.MinorVersion);
            r.PatchVersion.Should().Be(attr.PatchVersion);
            r.Applied.Should().BeWithin(TimeSpan.FromSeconds(2));


        }

        [TestCase]
        public void ApplyPatches()
        {
            ClearInvokations();
            var patches = EfPatchProcessor.FindAllPatches(new Assembly[] { this.GetType().Assembly }, "patchtest1");

            using var connection = SqliteDbConnectionFactory.CreateMemory();

            using (var query = connection.GetCreateEntityQuery<EfPatchHistoryRecord>())
                query.Execute();

            using (var query = connection.GetInsertEntityQuery<EfPatchHistoryRecord>())
            {
                var r = new EfPatchHistoryRecord()
                {
                    Scope = "patchtest1",
                    MajorVersion = 1,
                    MinorVersion = 1,
                    PatchVersion = 1,
                    Applied = new DateTime(2020, 1, 1)
                };
                query.Execute(r);

                r = new EfPatchHistoryRecord()
                {
                    Scope = "patchtest1",
                    MajorVersion = 1,
                    MinorVersion = 2,
                    PatchVersion = 1,
                    Applied = new DateTime(2020, 1, 2)
                };
                query.Execute(r);
            }

            connection.ApplyPatches(patches, "patchtest1");

            Count<Patch111>().Should().Be(0);
            Count<Patch112>().Should().Be(0);
            Count<Patch121>().Should().Be(0);

            var c = connection.GetAllPatches("patchtest1");

            Match<Patch122>(c, 2);
            Match<Patch123>(c, 3);
            Match<Patch211>(c, 4);
            Match<Patch221>(c, 5);
            Match<Patch321>(c, 6);

        }
    }
}
