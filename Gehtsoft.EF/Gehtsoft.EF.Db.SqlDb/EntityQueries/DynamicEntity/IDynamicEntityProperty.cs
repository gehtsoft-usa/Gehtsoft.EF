using System;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public interface IDynamicEntityProperty
    {
        Type PropertyType { get;  }
        string Name { get;  }
        EntityPropertyAttribute EntityPropertyAttribute { get;  }
        ObsoleteEntityPropertyAttribute ObsoleteEntityPropertyAttribute { get;  }
    }
}