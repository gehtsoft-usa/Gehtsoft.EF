using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Gehtsoft.EF.Serialization.IO
{
    public static class TextFormatter
    {
        public static string Format(short value) => value.ToString(CultureInfo.InvariantCulture);
        public static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
        public static string Format(float value) => value.ToString(CultureInfo.InvariantCulture);
        public static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);
        public static string Format(bool value) => value ? "true" : "false";
        public static string Format(DateTime value) => (value.Hour != 0 || value.Minute != 0 || value.Second != 0 || value.Millisecond != 0) ? value.ToString("O", CultureInfo.InvariantCulture) : value.ToString("d", CultureInfo.InvariantCulture);
        public static string Format(decimal value) => value.ToString(CultureInfo.InvariantCulture);
        public static string Format(byte[] value) => Convert.ToBase64String(value);

        public static bool Format(object value, out string formatted, out string type)
        {
            if (value == null)
            {
                formatted = "";
                type = "n";
                return true;
            }

            Type t1 = Nullable.GetUnderlyingType(value.GetType());

            if (t1 != null && t1 != value.GetType())
                value = Convert.ChangeType(value, t1);

            if (value is string t)
            {
                type = "t";
                formatted = t;
                return true;
            }

            if (value is short s)
            {
                type = "s";
                formatted = Format(s);
                return true;
            }

            if (value is int i)
            {
                type = "i";
                formatted = Format(i);
                return true;
            }

            if (value is float f)
            {
                type = "f";
                formatted = Format(f);
                return true;
            }

            if (value is double r)
            {
                type = "r";
                formatted = Format(r);
                return true;
            }

            if (value is bool b)
            {
                type = "b";
                formatted = Format(b);
                return true;
            }

            if (value is DateTime d)
            {
                type = "d";
                formatted = Format(d);
                return true;
            }

            if (value is decimal c)
            {
                type = "c";
                formatted = Format(c);
                return true;
            }

            if (value is byte[] l)
            {
                type = "l";
                formatted = Format(l);
                return true;
            }

            if (value.GetType().GetTypeInfo().IsEnum)
            {
                type = "i";
                formatted = Format((int)value);
                return true;
            }

            throw new ArgumentException("Type isn't supported", nameof(value));
        }

        public static short ParseShort(string value) => (short)Int32.Parse(value, CultureInfo.InvariantCulture);
        public static int ParseInt(string value) => Int32.Parse(value, CultureInfo.InvariantCulture);
        public static float ParseFloat(string value) => Single.Parse(value, CultureInfo.InvariantCulture);
        public static double ParseDouble(string value) => Double.Parse(value, CultureInfo.InvariantCulture);
        public static bool ParseBool(string value) => value == "true";
        public static DateTime ParseDateTime(string value) => DateTime.Parse(value, CultureInfo.InvariantCulture);
        public static decimal ParseDecimal(string value) => Decimal.Parse(value, CultureInfo.InvariantCulture);
        public static byte[] ParseByteArray(string value) => Convert.FromBase64String(value);

        public static object Parse(string type, string value)
        {
            if (type == "n")
                return null;
            if (type == "t")
                return value;
            if (type == "s")
                return ParseShort(value);
            if (type == "i")
                return ParseInt(value);
            if (type == "f")
                return ParseFloat(value);
            if (type == "r")
                return ParseDouble(value);
            if (type == "b")
                return ParseBool(value);
            if (type == "d")
                return ParseDateTime(value);
            if (type == "c")
                return ParseDecimal(value);
            if (type == "l")
                return ParseByteArray(value);

            throw new ArgumentException($"Unknown type code {type}", nameof(type));
        }

        public static T ParseAndConvert<T>(string typeCode, string value) => (T)ParseAndConvert(typeCode, value, typeof(T));

        public static object ParseAndConvert(string typeCode, string value, Type type)
        {
            TypeInfo typeInfo = type.GetTypeInfo();
            object v = Parse(typeCode, value);

            if (v == null)
            {
                if (typeInfo.IsValueType)
                    return Activator.CreateInstance(type);
                else
                    return null;
            }

            Type t1 = Nullable.GetUnderlyingType(type);
            if (t1 != null && t1 != type)
            {
                type = t1;
                typeInfo = type.GetTypeInfo();
            }

            if (v.GetType() != type)
            {
                if (typeInfo.IsEnum)
                    v = Enum.ToObject(type, v);
                else
                    v = Convert.ChangeType(v, type);
            }

            return v;
        }
    }
}
