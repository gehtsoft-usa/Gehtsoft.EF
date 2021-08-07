using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Entities.Context
{
    public interface IContextCount : IContextQueryWithCondition
    {
        int GetCount();

        Task<int> GetCountAsync(CancellationToken? token = null);
    }
}