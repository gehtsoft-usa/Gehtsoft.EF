using System;
using System.Collections.Generic;
using System.Text;

namespace Gehtsoft.EF.Utils
{
    public static class TypeConverter
    {
        public static object Convert(object value, Type type, IFormatProvider provider)
        {
            if (value == null)
            {
                if (type.IsValueType)
                    return Activator.CreateInstance(type);
                return null;
            }

            type = Nullable.GetUnderlyingType(type) ?? type;

            var valueType = value.GetType();
            if (valueType == type || type.IsInstanceOfType(value))
                return value;

            if (type.IsEnum)
            {
                if (valueType == typeof(short) ||
                    valueType == typeof(ushort) ||
                    valueType == typeof(int) ||
                    valueType == typeof(uint) ||
                    valueType == typeof(byte) ||
                    valueType == typeof(long) ||
                    valueType == typeof(ulong))
                    return Enum.ToObject(type, value);
                if (value is string s)
                    return Enum.Parse(type, s);
            }

            if (type == typeof(DateTime) && value is double d1)
                return DateTime.FromOADate(d1);
            else if (type == typeof(DateTime) && value is long l)
                return new DateTime(l);
            else if (type == typeof(double) && value is DateTime dt1)
                return dt1.ToOADate();
            else if (type == typeof(long) && value is DateTime dt2)
                return dt2.Ticks;

            return System.Convert.ChangeType(value, type, provider);
        }
    }
}
