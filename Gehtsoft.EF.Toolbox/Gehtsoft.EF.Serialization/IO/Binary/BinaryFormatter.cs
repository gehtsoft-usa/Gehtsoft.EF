using System;
using System.IO;

namespace Gehtsoft.EF.Serialization.IO.Binary
{
    /// <summary>
    /// Compact binary codec for the scalar values supported by the serializer. Each value
    /// is written as a one-byte type code followed by its payload. The codes match the
    /// single-character codes used by <see cref="TextFormatter"/> so the formats stay in sync.
    /// </summary>
    public static class BinaryFormatter
    {
        // type codes (kept identical to TextFormatter's textual codes)
        private const byte NullCode = (byte)'n';
        private const byte StringCode = (byte)'t';
        private const byte ShortCode = (byte)'s';
        private const byte IntCode = (byte)'i';
        private const byte LongCode = (byte)'q';
        private const byte FloatCode = (byte)'f';
        private const byte DoubleCode = (byte)'r';
        private const byte BoolCode = (byte)'b';
        private const byte DateCode = (byte)'d';
        private const byte DecimalCode = (byte)'c';
        private const byte GuidCode = (byte)'g';
        private const byte BlobCode = (byte)'l';

        public static void Write(BinaryWriter writer, object value)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            if (value == null)
            {
                writer.Write(NullCode);
                return;
            }

            Type type = value.GetType();
            if (type.IsEnum)
            {
                value = Convert.ChangeType(value, Enum.GetUnderlyingType(type));
                type = value.GetType();
            }

            switch (value)
            {
                case string s:
                    writer.Write(StringCode);
                    writer.Write(s);
                    break;
                case short sh:
                    writer.Write(ShortCode);
                    writer.Write(sh);
                    break;
                case int i:
                    writer.Write(IntCode);
                    writer.Write(i);
                    break;
                case long l:
                    writer.Write(LongCode);
                    writer.Write(l);
                    break;
                case float f:
                    writer.Write(FloatCode);
                    writer.Write(f);
                    break;
                case double d:
                    writer.Write(DoubleCode);
                    writer.Write(d);
                    break;
                case bool b:
                    writer.Write(BoolCode);
                    writer.Write(b);
                    break;
                case DateTime dt:
                    writer.Write(DateCode);
                    writer.Write(dt.ToBinary());
                    break;
                case decimal c:
                    writer.Write(DecimalCode);
                    writer.Write(c);
                    break;
                case Guid g:
                    writer.Write(GuidCode);
                    writer.Write(g.ToByteArray());
                    break;
                case byte[] bytes:
                    writer.Write(BlobCode);
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                    break;
                default:
                    throw new ArgumentException($"Type {type.FullName} isn't supported", nameof(value));
            }
        }

        public static object Read(BinaryReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            byte code = reader.ReadByte();
            switch (code)
            {
                case NullCode:
                    return null;
                case StringCode:
                    return reader.ReadString();
                case ShortCode:
                    return reader.ReadInt16();
                case IntCode:
                    return reader.ReadInt32();
                case LongCode:
                    return reader.ReadInt64();
                case FloatCode:
                    return reader.ReadSingle();
                case DoubleCode:
                    return reader.ReadDouble();
                case BoolCode:
                    return reader.ReadBoolean();
                case DateCode:
                    return DateTime.FromBinary(reader.ReadInt64());
                case DecimalCode:
                    return reader.ReadDecimal();
                case GuidCode:
                    return new Guid(reader.ReadBytes(16));
                case BlobCode:
                    int length = reader.ReadInt32();
                    return reader.ReadBytes(length);
                default:
                    throw new ArgumentException($"Unknown type code {(char)code}");
            }
        }
    }
}
