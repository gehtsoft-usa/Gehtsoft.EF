using System;

namespace Gehtsoft.EF.Entities.Context
{
    public interface IEntityContextTransaction : IDisposable
    {
        void Commit();

        void Rollback();
    }
}