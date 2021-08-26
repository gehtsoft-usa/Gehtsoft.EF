using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Entities.Context
{
    /// <summary>
    /// The context count query.
    /// </summary>
    public interface IContextCount : IContextQueryWithCondition
    {
        /// <summary>
        /// Returns the number of the rows counted.
        /// </summary>
        /// <returns></returns>
        int GetCount();

        /// <summary>
        /// Returns the number of the rows counted asynchronously.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<int> GetCountAsync(CancellationToken? token = null);
    }
}