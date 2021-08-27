using System;
using System.Data;

namespace Gehtsoft.EF.Test.Utils.DummyDb
{
    internal class DummyDbDataReaderColumn
    {
        public string Name { get; }
        public DbType DbType { get; }

        internal DummyDbDataReaderColumn(string name, DbType type)
        {
            Name = name;
            DbType = type;
        }

        public Type Type
        {
            get => DbType switch
            {
                DbType.Boolean => typeof(bool),
                DbType.Int16 => typeof(short),
                DbType.Int32 => typeof(int),
                DbType.Int64 => typeof(long),
                DbType.String => typeof(string),
                DbType.StringFixedLength => typeof(string),
                DbType.Byte => typeof(byte),
                DbType.Currency => typeof(decimal),
                DbType.Decimal => typeof(decimal),
                DbType.VarNumeric => typeof(decimal),
                DbType.Binary => typeof(byte[]),
                DbType.Double => typeof(double),
                DbType.Date => typeof(DateTime),
                DbType.DateTime => typeof(DateTime),
                DbType.DateTime2 => typeof(DateTime),
                DbType.DateTimeOffset => typeof(TimeSpan),
                DbType.Guid => typeof(Guid),
                DbType.Time => typeof(TimeSpan),
                DbType.AnsiString => typeof(string),
                DbType.AnsiStringFixedLength => typeof(string),
                _ => throw new InvalidOperationException("Unknown type")
            };
        }
    }
}
