using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public interface IEntitySerializationCallback
    {
        void BeforeSerialization(SqlDbConnection connection);
        void AfterDeserealization(SelectEntitiesQueryBase query);
    }
}
