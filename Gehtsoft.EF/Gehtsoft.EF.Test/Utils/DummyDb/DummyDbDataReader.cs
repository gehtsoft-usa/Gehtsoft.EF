using System;
using System.Collections;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Test.Utils.DummyDb
{
    internal class DummyDbDataReader : DbDataReader
    {
        private int CurrentResult = 0;
        private int CurrentRow = -1;

        public DummyDbDataReaderResultCollection Results { get; set; } = new DummyDbDataReaderResultCollection();

        public void Add(DummyDbDataReaderResult result) => Results.Add(result);

        public override object this[int ordinal] => Results[CurrentResult].Data[CurrentRow][ordinal];

        public override object this[string name] => Results[CurrentResult].Data[CurrentRow][GetOrdinal(name)];

        public override int Depth => 1;

        public override int FieldCount => Results[CurrentResult].Columns.Count;

        public override bool HasRows => (CurrentResult < (Results?.Count ?? 0)) &&
                                        (CurrentRow < (Results[CurrentResult].Data?.Count ?? 0));

        public override bool IsClosed => false;

        public override int RecordsAffected => 0;

        public override bool GetBoolean(int ordinal)
        {
            return (bool)this[ordinal];
        }

        public override byte GetByte(int ordinal)
        {
            return (byte)this[ordinal];
        }

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            Array.Copy((byte[])this[ordinal], dataOffset, buffer, bufferOffset, length);
            return length;
        }

        public override char GetChar(int ordinal)
        {
            return (char)this[ordinal];
        }

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            Array.Copy(((string)this[ordinal]).ToCharArray(), dataOffset, buffer, bufferOffset, length);
            return 0;
        }

        public override string GetDataTypeName(int ordinal) => throw new NotImplementedException();

        public override DateTime GetDateTime(int ordinal)
        {
            return (DateTime)this[ordinal];
        }

        public override decimal GetDecimal(int ordinal)
        {
            return (decimal)this[ordinal];
        }

        public override double GetDouble(int ordinal)
        {
            return (double)this[ordinal];
        }

        public override IEnumerator GetEnumerator()
        {
            return Results[CurrentResult].Data[CurrentRow].GetEnumerator();
        }

        public override Type GetFieldType(int ordinal)
        {
            return Results[CurrentResult].Columns[ordinal].Type;
        }

        public override float GetFloat(int ordinal)
        {
            return (float)this[ordinal];
        }

        public override Guid GetGuid(int ordinal)
        {
            return (Guid)this[ordinal];
        }

        public override short GetInt16(int ordinal)
        {
            return (short)this[ordinal];
        }

        public override int GetInt32(int ordinal)
        {
            return (int)this[ordinal];
        }

        public override long GetInt64(int ordinal)
        {
            return (long)this[ordinal];
        }

        public override string GetName(int ordinal)
        {
            return Results[CurrentResult].Columns[ordinal].Name;
        }

        public override int GetOrdinal(string name)
        {
            for (int i = 0; i < Results[CurrentResult].Columns.Count; i++)
                if (string.Equals(name, Results[CurrentResult].Columns[i].Name, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        public override string GetString(int ordinal)
        {
            return (string)this[ordinal];
        }

        public override object GetValue(int ordinal)
        {
            return this[ordinal];
        }

        public override int GetValues(object[] values) => throw new NotImplementedException();

        public override bool IsDBNull(int ordinal)
        {
            return this[ordinal] == null || this[ordinal] == DBNull.Value;
        }

        public bool NextResultCalled { get; set; }

        public override bool NextResult()
        {
            NextResultCalled = true;
            if (CurrentResult >= Results.Count - 1)
                return false;
            CurrentResult++;
            CurrentRow = -1;
            return true;

        }
        public bool NextResultAsyncCalled { get; set; }

        public override Task<bool> NextResultAsync(CancellationToken cancellationToken)
        {
            NextResultAsyncCalled = true;
            if (CurrentResult >= Results.Count - 1)
                return Task.FromResult(false);
            CurrentResult++;
            CurrentRow = -1;
            return Task.FromResult(true);
        }

        public bool ReadCalled { get; set; }

        public override bool Read()
        {
            ReadCalled = true;
            if (CurrentResult >= Results.Count)
                return false;
            if (CurrentRow >= Results[CurrentResult].Data.Count - 1)
                return false;
            CurrentRow++;
            return true;
        }

        public bool ReadAsyncCalled { get; set; }

        public override Task<bool> ReadAsync(CancellationToken cancellationToken) 
        {
            ReadAsyncCalled = true;
            if (CurrentResult >= Results.Count)
                return Task.FromResult(false);
            if (CurrentRow >= Results[CurrentResult].Data.Count - 1)
                return Task.FromResult(false);
            CurrentRow++;
            return Task.FromResult(true);
        }

        public bool DisposeCalled { get; set; }

        protected override void Dispose(bool disposing)
        {
            DisposeCalled = true;
            base.Dispose(disposing);
        }
    }
}
