using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Entities.Context
{
    public interface IEntityQuery : IDisposable
    {
        int Execute();

        Task<int> ExecuteAsync(CancellationToken? token = null);
    }
}