using System;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// Event arguments for entity discovered event.
    ///
    /// See also: <see cref="AllEntities.OnEntityDiscovered"/>
    /// </summary>
    public class EntityDescriptorEventArgs : EventArgs
    {
        public EntityDescriptor Entity { get; }
        public EntityDescriptorEventArgs(EntityDescriptor entity)
        {
            Entity = entity;
        }
    }
}
