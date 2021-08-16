using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.EF.Test.Utils.DummyDb
{
    internal class DummyDbDataReaderColumnDataRows : IEnumerable<DummyDbDataReaderColumnDataCollection>
    {
        private readonly List<DummyDbDataReaderColumnDataCollection> mRows = new List<DummyDbDataReaderColumnDataCollection>();

        public int Count => mRows.Count;

        public DummyDbDataReaderColumnDataCollection this[int index] => mRows[index];

        public void Add(DummyDbDataReaderColumnDataCollection row) => mRows.Add(row);

        public void Add(params object[] columns) => mRows.Add(new DummyDbDataReaderColumnDataCollection(columns));

        public IEnumerator<DummyDbDataReaderColumnDataCollection> GetEnumerator() => mRows.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => mRows.GetEnumerator();
    }
}
