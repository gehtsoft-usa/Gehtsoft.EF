using System;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class EntityDescriptorEventArgs : EventArgs
    {
        public EntityDescriptor Entity { get; }
        public EntityDescriptorEventArgs(EntityDescriptor entity)
        {
            Entity = entity;
        }
    }
}
