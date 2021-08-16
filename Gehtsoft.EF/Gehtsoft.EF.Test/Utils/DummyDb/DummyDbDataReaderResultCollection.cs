using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.EF.Test.Utils.DummyDb
{
    internal class DummyDbDataReaderResultCollection : IEnumerable<DummyDbDataReaderResult>
    {
        private readonly List<DummyDbDataReaderResult> mResults = new();

        public int Count => mResults.Count;
        public DummyDbDataReaderResult this[int index] => mResults[index];

        public IEnumerator<DummyDbDataReaderResult> GetEnumerator() => mResults.GetEnumerator();

        public void Add(DummyDbDataReaderResult column) => mResults.Add(column);

        IEnumerator IEnumerable.GetEnumerator() => mResults.GetEnumerator();
    }
}
