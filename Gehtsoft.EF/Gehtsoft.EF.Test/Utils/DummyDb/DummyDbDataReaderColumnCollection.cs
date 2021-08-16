using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace Gehtsoft.EF.Test.Utils.DummyDb
{
    internal class DummyDbDataReaderColumnCollection : IEnumerable<DummyDbDataReaderColumn>
    {
        private readonly List<DummyDbDataReaderColumn> mColumns = new ();

        public int Count => mColumns.Count;
        public DummyDbDataReaderColumn this[int index] => mColumns[index];

        public IEnumerator<DummyDbDataReaderColumn> GetEnumerator() => mColumns.GetEnumerator();

        public void Add(DummyDbDataReaderColumn column) => mColumns.Add(column);

        public void Add(string name, DbType type) => mColumns.Add(new DummyDbDataReaderColumn(name, type));

        IEnumerator IEnumerable.GetEnumerator() => mColumns.GetEnumerator();
    }
}
