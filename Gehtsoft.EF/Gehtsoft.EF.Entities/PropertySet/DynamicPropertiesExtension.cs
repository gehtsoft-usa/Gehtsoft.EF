using System;
using System.Collections.Generic;
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
            DynamicPropertyBag bag = new DynamicPropertyBag { IsNew = true };
            AttachBag(entity, bag);
            return bag;
        }

        /// <summary>
        /// Attaches a loaded dynamic property bag to an entity read from the database: the supplied
        /// properties become the bag's contents and its change-tracking baseline (nothing is reported
        /// as modified, and the bag is <b>not</b> flagged as new). Assigned reflectively, like
        /// <see cref="InitializeDynamicProperties(object)"/>.
        /// </summary>
        /// <param name="entity">The entity, which must implement <see cref="IDynamicPropertiesOwner"/>.</param>
        /// <param name="properties">The properties loaded from the side table.</param>
        /// <returns>The attached, loaded bag.</returns>
        public static DynamicPropertyBag LoadDynamicProperties(this object entity, IEnumerable<(string Name, object Value)> properties)
        {
            DynamicPropertyBag bag = new DynamicPropertyBag();
            bag.Initialize(properties);
            AttachBag(entity, bag);
            return bag;
        }

        private static void AttachBag(object entity, DynamicPropertyBag bag)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (!(entity is IDynamicPropertiesOwner))
                throw new ArgumentException($"The type '{entity.GetType().FullName}' does not implement {nameof(IDynamicPropertiesOwner)}", nameof(entity));

            PropertyInfo property = entity.GetType().GetProperty(nameof(IDynamicPropertiesOwner.DynamicProperties), BindingFlags.Public | BindingFlags.Instance);
            if (property == null || property.SetMethod == null)
                throw new InvalidOperationException($"The type '{entity.GetType().FullName}' does not expose a settable '{nameof(IDynamicPropertiesOwner.DynamicProperties)}' property");

            property.SetValue(entity, bag);
        }
    }
}
