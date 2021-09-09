using System;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The attribute to set the action to be called when property is created.
    ///
    /// The action will be called only if the entity is created using
    /// <see cref="CreateEntityController"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class OnEntityPropertyCreateAttribute : OnEntityActionAttribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="containerType">The type that consists of action method.</param>
        /// <param name="delegateName">The action method name. The method should match either <see cref="EntityActionDelegate"/></param>
        public OnEntityPropertyCreateAttribute(Type containerType, string delegateName) : base(containerType, delegateName)
        {
        }
    }
}
