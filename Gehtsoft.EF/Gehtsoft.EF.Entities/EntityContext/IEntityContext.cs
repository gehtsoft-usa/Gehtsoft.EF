using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Gehtsoft.EF.Entities.Context
{
    public interface IEntityContext : IDisposable
    {
        IEntityTable[] ExistingTables();

        IEntityQuery DropEntity(Type type);

        IEntityQuery CreateEntity(Type type);

        IModifyEntityQuery InsertEntity(Type type, bool createKey);

        IModifyEntityQuery UpdateEntity(Type type);

        IModifyEntityQuery DeleteEntity(Type type);

        IContextQueryWithCondition DeleteMultiple(Type type);

        IContextSelect Select(Type type);

        IContextCount Count(Type type);

        IEntityContextTransaction BeginTransaction();
    }
}