using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.CreateEntity.Patch
{
    public interface IEfPatchAsync : IEfPatch
    {
        Task ApplyAsync(SqlDbConnection connection);
    }

}
