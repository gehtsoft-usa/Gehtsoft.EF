using System;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The attribute to set the action to be called when property is dropped.
    ///
    /// The action will be called only if the property is dropped using
    /// <see cref="CreateEntityController"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class OnEntityPropertyDropAttribute : OnEntityActionAttribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="containerType">The type that consists of action method.</param>
        /// <param name="delegateName">The action method name. The method should match either <see cref="EntityActionDelegate"/></param>
        public OnEntityPropertyDropAttribute(Type containerType, string delegateName) : base(containerType, delegateName)
        {
        }
    }
}
