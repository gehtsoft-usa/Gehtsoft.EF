using System;
using System.Globalization;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The storage type of a dynamic property value.
    ///
    /// It selects both the EAV value column a query targets (String -> `v_str`; Integer / Long /
    /// Boolean / DateTime -> `v_int`; Real -> `v_real`) and how a stored value is decoded back to its
    /// CLR form. It is passed explicitly when a dynamic property is projected, ordered, grouped or
    /// aggregated in a query - unlike a WHERE filter, those clauses have no CLR operand to infer the
    /// type from.
    ///
    /// The numeric values are the codes stored in the `prop_type` column and MUST remain stable (the
    /// load path decodes by them).
    /// </summary>
    public enum DynamicPropertyValueType
    {
        /// <summary>A string value, stored in `v_str`.</summary>
        String = 0,
        /// <summary>A 32-bit integer value, stored in `v_int`.</summary>
        Integer = 1,
        /// <summary>A 64-bit integer value, stored in `v_int`.</summary>
        Long = 2,
        /// <summary>A double-precision value, stored in `v_real`.</summary>
        Real = 3,
        /// <summary>A boolean value, stored as 0/1 in `v_int`.</summary>
        Boolean = 4,
        /// <summary>A date/time value, stored as UTC ticks in `v_int`.</summary>
        DateTime = 5,
    }

    /// <summary>
    /// Maps dynamic property values between their CLR form and the SQL EAV storage form
    /// (a `prop_type` code plus one of the `v_str` / `v_int` / `v_real` columns).
    ///
    /// This is the SQL back-end's type model - a different back-end (e.g. MongoDB) would map values
    /// its own way.
    /// </summary>
    internal static class DynamicPropertiesValueMapper
    {
        /// <summary>
        /// Encodes a CLR value into its storage form: the type code, the target value column, and
        /// the value to store in that column.
        /// </summary>
        /// <param name="value">The CLR value (not `null`).</param>
        public static (DynamicPropertyValueType Type, string Column, object Value) Encode(object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            switch (value)
            {
                case string s:
                    return (DynamicPropertyValueType.String, DynamicPropertiesTableBuilder.StringValueColumn, s);
                case bool b:
                    return (DynamicPropertyValueType.Boolean, DynamicPropertiesTableBuilder.IntValueColumn, b ? 1L : 0L);
                case int i:
                    return (DynamicPropertyValueType.Integer, DynamicPropertiesTableBuilder.IntValueColumn, (long)i);
                case long l:
                    return (DynamicPropertyValueType.Long, DynamicPropertiesTableBuilder.IntValueColumn, l);
                case double d:
                    return (DynamicPropertyValueType.Real, DynamicPropertiesTableBuilder.RealValueColumn, d);
                case DateTime dt:
                    return (DynamicPropertyValueType.DateTime, DynamicPropertiesTableBuilder.IntValueColumn, dt.ToUniversalTime().Ticks);
                default:
                    throw new ArgumentException($"The type '{value.GetType().FullName}' is not a supported dynamic property value type", nameof(value));
            }
        }

        /// <summary>
        /// Decodes a stored value (read from the column selected by <paramref name="type"/>) back to
        /// its CLR form.
        /// </summary>
        /// <param name="type">The stored type code.</param>
        /// <param name="value">The stored column value (not `null`).</param>
        public static object Decode(DynamicPropertyValueType type, object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            switch (type)
            {
                case DynamicPropertyValueType.String:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
                case DynamicPropertyValueType.Integer:
                    return (int)Convert.ToInt64(value, CultureInfo.InvariantCulture);
                case DynamicPropertyValueType.Long:
                    return Convert.ToInt64(value, CultureInfo.InvariantCulture);
                case DynamicPropertyValueType.Real:
                    return Convert.ToDouble(value, CultureInfo.InvariantCulture);
                case DynamicPropertyValueType.Boolean:
                    return Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0L;
                case DynamicPropertyValueType.DateTime:
                    return new DateTime(Convert.ToInt64(value, CultureInfo.InvariantCulture), DateTimeKind.Utc);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }
}
