using System;
using System.Collections.Generic;
using System.Text;

namespace Gehtsoft.EF.Serialization.IO
{
    public delegate void TypeStartedDelegate(Type type);
    public delegate void EntityDelegate(object entity);

    public interface IEntityReader
    {
        event TypeStartedDelegate OnTypeStarted;
        event EntityDelegate OnEntity;

        void Scan();

    }
}
