using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Entities.Context
{
    /// <summary>
    /// The query that selects the entities according the filter specified.
    /// </summary>
    public interface IContextSelect : IContextQueryWithCondition
    {
        /// <summary>
        /// The order definition.
        /// </summary>
        IContextOrder Order { get; }

        /// <summary>
        /// The number of the records to take.
        ///
        /// If value is not specified, all records will be read.
        /// </summary>
        int? Take { get; set; }

        /// <summary>
        /// The number of records to skip.
        /// </summary>
        int? Skip { get; set; }

        /// <summary>
        /// Reads one entity.
        ///
        /// If there is no more entities, the method returns `null`.
        /// </summary>
        /// <returns></returns>
        object ReadOne();

        /// <summary>
        /// Reads one entity asynchronously.
        ///
        /// If there is no more entities, the task will return `null`.
        /// </summary>
        Task<object> ReadOneAsync(CancellationToken? token = null);
    }
}