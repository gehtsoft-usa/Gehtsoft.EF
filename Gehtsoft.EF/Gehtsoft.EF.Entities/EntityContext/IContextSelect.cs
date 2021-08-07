using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Entities.Context
{
    public interface IContextSelect : IContextQueryWithCondition
    {
        IContextOrder Order { get; }

        int? Take { get; set; }
        int? Skip { get; set; }

        object ReadOne();

        Task<object> ReadOneAsync(CancellationToken? token = null);
    }
}