using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Entities.Context
{
    /// <summary>
    /// The query that involves modification of the query object.
    /// </summary>
    public interface IModifyEntityQuery : IDisposable
    {
        /// <summary>
        /// Executes the operation for the object specified.
        /// </summary>
        /// <param name="entity"></param>
        void Execute(object entity);

        /// <summary>
        /// Executes the operation of the object specified asynchronously.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task ExecuteAsync(object entity, CancellationToken? token = null);
    }
}