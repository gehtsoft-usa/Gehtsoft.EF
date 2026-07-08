namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// Implemented by an entity that owns a dynamic property set.
    ///
    /// The contract exposes the bag as **read-only** so client code cannot assign it by mistake
    /// (and cannot clobber a loaded bag). The implementing entity is expected to back it with a
    /// private setter, e.g. `public DynamicPropertyBag DynamicProperties { get; private set; }`;
    /// a driver populates it by setting that private member via reflection when dynamic properties
    /// are loaded.
    ///
    /// The bag is `null` until it is set. A `null` bag means "not loaded / not set", so accessing
    /// it then fails loudly rather than silently presenting an empty set (which would hide a
    /// missing load). The owning entity type is expected to be marked with
    /// <see cref="DynamicPropertiesAttribute"/>.
    /// </summary>
    public interface IDynamicPropertiesOwner
    {
        /// <summary>
        /// The dynamic property bag, or `null` if it has not been loaded or set.
        /// </summary>
        DynamicPropertyBag DynamicProperties { get; }
    }
}
