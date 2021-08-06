using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Gehtsoft.EF.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.CreateEntity.Patch
{
    public static class EfPatchProcessor 
    {
        public class EfPatchInstance
        {
            public EfPatchAttribute Version { get; }
            public Type PatchType { get; }

            public EfPatchInstance(EfPatchAttribute version, Type type)
            {
                Version = version;
                PatchType = type;
            }

            public object Create(IServiceProvider serviceProvider = null)
            {
                if (serviceProvider != null)
                    return ActivatorUtilities.CreateInstance(serviceProvider, PatchType);
                else
                    return Activator.CreateInstance(PatchType);
            }
        }

        internal class EfPatchInstanceComparer : IComparer<EfPatchInstance>
        {
            public int Compare(EfPatchInstance x, EfPatchInstance y)
            {
                if (x == null && y == null)
                    return 0;
                if (x == null)
                    return -1;
                if (y == null)
                    return 1;

                int xv = x.Version.MajorVersion * 10000000 +
                         x.Version.MinorVersion * 10000 +
                         x.Version.PatchVersion;

                int yv = y.Version.MajorVersion * 10000000 +
                         y.Version.MinorVersion * 10000 +
                         y.Version.PatchVersion;

                if (xv > yv)
                    return 1;
                else if (xv < yv)
                    return -1;
                return 0;
            }
        }

        public static IList<EfPatchInstance> FindAllPatches(IEnumerable<Assembly> assemblies, string scope = null)
        {
            List<EfPatchInstance> list = new List<EfPatchInstance>();
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    var attr = type.GetCustomAttribute<EfPatchAttribute>();
                    if (attr != null && type.GetInterfaces().Any(i => i == typeof(IEfPatch)))
                    {
                        if (!string.IsNullOrEmpty(scope) && attr.Scope != scope)
                            continue;
                        list.Add(new EfPatchInstance(attr, type));
                    }
                }
            }
            list.Sort(new EfPatchInstanceComparer());
            return list;
        }

        private static async ValueTask<EntityCollection<EfPatchHistoryRecord>> GetAllPatchesCore(SqlDbConnection connection, bool @async, string scope, int? take = null, int? skip = null)
        {
            using (var query = connection.GetSelectEntitiesQuery<EfPatchHistoryRecord>())
            {
                if (!string.IsNullOrEmpty(scope))
                    query.Where.Property(nameof(EfPatchHistoryRecord.Scope)).Eq(scope);

                query.AddOrderBy(nameof(EfPatchHistoryRecord.Applied));
                if (take != null)
                    query.Limit = take.Value;
                if (skip != null)
                    query.Skip = skip.Value;
                if (@async)
                    return await query.ReadAllAsync<EfPatchHistoryRecord>();
                else
                    return query.ReadAll<EfPatchHistoryRecord>();
            }
        }

        public static EntityCollection<EfPatchHistoryRecord> GetAllPatches(this SqlDbConnection connection, string scope, int? take = null, int? skip = null)
            => GetAllPatchesCore(connection, false, scope, take, skip).Result;

        public static Task<EntityCollection<EfPatchHistoryRecord>> GetAllPatchesAsync(this SqlDbConnection connection, string scope, int? take = null, int? skip = null)
            => GetAllPatchesCore(connection, true, scope, take, skip).AsTask();

        private static async ValueTask<EfPatchHistoryRecord> GetLastAppliedPatchCore(SqlDbConnection connection, bool @async, string scope)
        {
            using (var query = connection.GetSelectEntitiesQuery<EfPatchHistoryRecord>())
            {
                query.Where.Property(nameof(EfPatchHistoryRecord.Scope)).Eq(scope);
                query.AddOrderBy(nameof(EfPatchHistoryRecord.Applied), SortDir.Desc);
                query.Limit = 1;
                if (@async)
                    return await query.ReadOneAsync<EfPatchHistoryRecord>();
                else
                    return query.ReadOne<EfPatchHistoryRecord>();
            }
        }

        public static EfPatchHistoryRecord GetLastAppliedPatch(this SqlDbConnection connection, string scope)
            => GetLastAppliedPatchCore(connection, false, scope).Result;

        public static Task<EfPatchHistoryRecord> GetLastAppliedPatchAsync(this SqlDbConnection connection, string scope)
            => GetLastAppliedPatchCore(connection, true, scope).AsTask();

        private static async ValueTask<bool> SaveLastPatchCore(SqlDbConnection connection, bool @async, EfPatchInstance patch)
        {
            using (var query = connection.GetInsertEntityQuery<EfPatchHistoryRecord>())
            {
                var r = new EfPatchHistoryRecord(patch.Version.Scope, patch.Version.MajorVersion, patch.Version.MinorVersion, patch.Version.PatchVersion, DateTime.Now);
                if (@async)
                    await query.ExecuteAsync(r);
                else
                    query.Execute(r);

                return true;
            }
        }

        private static Task SavePatchAsync(SqlDbConnection connection, EfPatchInstance patch)
            => SaveLastPatchCore(connection, true, patch).AsTask();

        private static void SavePatch(SqlDbConnection connection, EfPatchInstance patch)
        {
            if (!SaveLastPatchCore(connection, false, patch).IsCompleted)
                throw new InvalidOperationException("Sync task expected to be already completed");
        }

        private static async ValueTask ApplyPatchesCore(SqlDbConnection connection, bool @async, IList<EfPatchInstance> patches, string scope, IServiceProvider serviceProvider)
        {
            var patchEntityTable = AllEntities.Inst[typeof(EfPatchHistoryRecord)];
            var schema = @async ? (await connection.SchemaAsync()) : connection.Schema();

            if (!schema.Any(t => t.Name.Equals(patchEntityTable.TableDescriptor.Name)))
            {
                var createTableBuilder = connection.GetCreateTableBuilder(patchEntityTable.TableDescriptor);
                using (var query = connection.GetQuery(createTableBuilder))
                {
                    if (@async)
                        await query.ExecuteNoDataAsync();
                    else
                        query.ExecuteNoData();
                }

                if (patches.Count > 0)
                {
                    if (@async)
                        await SavePatchAsync(connection, patches[patches.Count - 1]);
                    else
                        SavePatch(connection, patches[patches.Count - 1]);
                }
            }
            else
            {
                if (patches != null)
                {
                    var lastApplied = @async ? await connection.GetLastAppliedPatchAsync(scope) : connection.GetLastAppliedPatch(scope);
                    int? startFrom = null;
                    if (lastApplied != null)
                    {
                        int l = lastApplied.MajorVersion * 10000000 +
                                 lastApplied.MinorVersion * 10000 +
                                 lastApplied.PatchVersion;


                        for (int i = 0; i < patches.Count && startFrom == null; i++)
                        {
                            int v = patches[i].Version.MajorVersion * 10000000 +
                                     patches[i].Version.MinorVersion * 10000 +
                                     patches[i].Version.PatchVersion;

                            if (v > l)
                                startFrom = i;

                        }
                    }

                    if (startFrom != null)
                    {
                        for (int i = startFrom.Value; i < patches.Count; i++)
                        {
                            var patch = patches[i].Create(serviceProvider);
                            if (async && patch is IEfPatchAsync asyncPath)
                                await asyncPath.ApplyAsync(connection);
                            else
                                (patch as IEfPatch).Apply(connection);

                            if (@async)
                                await SavePatchAsync(connection, patches[i]);
                            else
                                SavePatch(connection, patches[i]);

                        }
                    }
                }
            }
        }

        public static Task ApplyPatchesAsync(this SqlDbConnection connection, IList<EfPatchInstance> patches, string scope, IServiceProvider serviceProvider = null)
            => ApplyPatchesCore(connection, true, patches, scope, serviceProvider).AsTask();

        public static void ApplyPatches(this SqlDbConnection connection, IList<EfPatchInstance> patches, string scope, IServiceProvider serviceProvider = null)
        {
            if (!ApplyPatchesCore(connection, false, patches, scope, serviceProvider).IsCompleted)
                throw new InvalidOperationException("Sync task expected to be already completed");
        }
    }
}
