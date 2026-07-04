using System;
using System.Reflection;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// Extension methods for working with an entity's dynamic property bag.
    /// </summary>
    public static class DynamicPropertiesExtension
    {
        /// <summary>
        /// Attaches a fresh, empty dynamic property bag to a new (not-yet-inserted) entity and
        /// returns it, so the caller can populate dynamic properties before inserting the entity.
        ///
        /// The bag is flagged as new (<see cref="DynamicPropertyBag.IsNew"/>). Because the bag is
        /// exposed read-only on <see cref="IDynamicPropertiesOwner"/>, it is assigned by setting
        /// the entity's private setter reflectively.
        /// </summary>
        /// <param name="entity">The entity, which must implement <see cref="IDynamicPropertiesOwner"/>.</param>
        /// <returns>The newly attached bag.</returns>
        public static DynamicPropertyBag InitializeDynamicProperties(this object entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (!(entity is IDynamicPropertiesOwner))
                throw new ArgumentException($"The type '{entity.GetType().FullName}' does not implement {nameof(IDynamicPropertiesOwner)}", nameof(entity));

            PropertyInfo property = entity.GetType().GetProperty(nameof(IDynamicPropertiesOwner.DynamicProperties), BindingFlags.Public | BindingFlags.Instance);
            if (property == null || property.SetMethod == null)
                throw new InvalidOperationException($"The type '{entity.GetType().FullName}' does not expose a settable '{nameof(IDynamicPropertiesOwner.DynamicProperties)}' property");

            DynamicPropertyBag bag = new DynamicPropertyBag { IsNew = true };
            property.SetValue(entity, bag);
            return bag;
        }
    }
}
