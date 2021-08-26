using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Entities.Context
{
    /// <summary>
    /// The query to the entity.
    /// </summary>
    public interface IEntityQuery : IDisposable
    {
        /// <summary>
        /// Executes the query.
        /// </summary>
        /// <returns></returns>
        int Execute();

        /// <summary>
        /// Executes query asynchronously.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<int> ExecuteAsync(CancellationToken? token = null);
    }
}