namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.CreateEntity.Patch
{
    public interface IEfPatch
    {
        void Apply(SqlDbConnection connection);
    }
}
