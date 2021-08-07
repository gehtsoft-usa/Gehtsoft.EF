using System;
using System.Data;

namespace Gehtsoft.EF.Entities
{
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class AutoIdAttribute : EntityPropertyAttribute
    {
        public AutoIdAttribute()
        {
            DbType = DbType.Int32;
            AutoId = true;
        }
    }
}
