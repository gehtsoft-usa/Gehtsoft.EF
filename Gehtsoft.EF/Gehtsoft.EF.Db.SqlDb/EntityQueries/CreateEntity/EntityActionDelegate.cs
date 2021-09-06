using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The delegate type of the action called when entity is created.
    ///
    /// The delegate prototype is:
    /// ```cs
    /// public delegate void EntityActionDelegate(SqlDbConnection connection);
    /// ```
    ///
    /// The action is invoked only when <see cref="CreateEntityController"/> is used
    /// to create the entity.
    ///
    /// To set the action use <see cref="OnEntityCreateAttribute"/>, <see cref="OnEntityDropAttribute"/>,
    /// <see cref="OnEntityPropertyCreateAttribute"/> or <see cref="OnEntityPropertyDropAttribute"/>.
    /// </summary>
    /// <param name="connection"></param>
    public delegate void EntityActionDelegate(SqlDbConnection connection);
}
