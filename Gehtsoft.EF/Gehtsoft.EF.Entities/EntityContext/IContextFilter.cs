using System;

namespace Gehtsoft.EF.Entities.Context
{
    public interface IContextFilter
    {
        IDisposable AddGroup(LogOp logOp = LogOp.And);

        IContextFilterCondition Add(LogOp op = LogOp.And);
    }
}