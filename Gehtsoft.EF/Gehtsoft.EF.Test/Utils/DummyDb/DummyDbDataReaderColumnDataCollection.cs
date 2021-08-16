using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace Gehtsoft.EF.Test.Utils.DummyDb
{
    internal class DummyDbDataReaderColumnDataCollection : IEnumerable<object>
    {
        private readonly List<object> mColumns = new();

        public DummyDbDataReaderColumnDataCollection(params object[] args)
        {
            mColumns.AddRange(args);
        }

        public int Count => mColumns.Count;
        public object this[int index] => mColumns[index];

        public IEnumerator<object> GetEnumerator() => mColumns.GetEnumerator();

        public void Add(object column) => mColumns.Add(column);
        public void Add(IEnumerable<object> column) => mColumns.AddRange(column);

        IEnumerator IEnumerable.GetEnumerator() => mColumns.GetEnumerator();
    }
}
