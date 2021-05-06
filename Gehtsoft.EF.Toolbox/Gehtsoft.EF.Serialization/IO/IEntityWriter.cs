using System;
using System.Collections.Generic;
using System.Text;

namespace Gehtsoft.EF.Serialization.IO
{
    public interface IEntityWriter
    {
        void Start(Type type);
        void Write(object entity);
    }
}
