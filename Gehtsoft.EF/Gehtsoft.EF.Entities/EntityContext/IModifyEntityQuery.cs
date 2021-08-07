using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Entities.Context
{
    public interface IModifyEntityQuery : IDisposable
    {
        void Execute(object entity);

        Task ExecuteAsync(object entity, CancellationToken? token = null);
    }
}