using System;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// The attribute that marks an entity as an owner of a dynamic property set.
    ///
    /// A dynamic property set is a per-row, flat bag of named values of simple types
    /// (string, integer, double, date/time, boolean) that can be set and read by name and
    /// used in queries. The values are stored in a supplemental side table named
    /// `&lt;owner_table&gt;<see cref="TableSuffix"/>`.
    ///
    /// The attribute also carries the shaping options for that side table.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class DynamicPropertiesAttribute : Attribute
    {
        /// <summary>
        /// The fixed suffix appended to the owner table name to build the property side-table
        /// name (`&lt;owner_table&gt;_props`).
        ///
        /// It is a constant (not configurable) so the side table can always be located by name
        /// - in particular when reconciling schema after the attribute has been removed from an
        /// entity.
        /// </summary>
        public const string TableSuffix = "_props";

        /// <summary>
        /// The size of the property name column.
        ///
        /// The default value is `64`.
        /// </summary>
        public int NameSize { get; set; } = 64;

        /// <summary>
        /// The size of the string value column.
        ///
        /// The default value is `256`.
        /// </summary>
        public int StringValueSize { get; set; } = 256;

        /// <summary>
        /// The size (total number of digits) of the real (floating-point) value column.
        ///
        /// The default value is `-1`, which lets the driver select its native default for the
        /// column type.
        /// </summary>
        public int RealValueSize { get; set; } = -1;

        /// <summary>
        /// The precision (number of digits after the decimal point) of the real
        /// (floating-point) value column.
        ///
        /// The default value is `-1`, which lets the driver select its native default for the
        /// column type.
        /// </summary>
        public int RealValuePrecision { get; set; } = -1;
    }
}
