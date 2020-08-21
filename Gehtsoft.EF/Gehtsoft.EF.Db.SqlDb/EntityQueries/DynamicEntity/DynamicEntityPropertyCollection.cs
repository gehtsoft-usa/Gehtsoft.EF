using System;
using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class DynamicEntityPropertyCollection : IList<IDynamicEntityProperty>
    {
        private const string READONLY = "Collection is readonly";

        private List<IDynamicEntityProperty> mProperties = new List<IDynamicEntityProperty>();

        public bool IsReadOnly { get; private set; } = false;

        public DynamicEntityPropertyCollection()
        {
            IsReadOnly = false;
        }

        public DynamicEntityPropertyCollection(IEnumerable<IDynamicEntityProperty> source)
        {
            IsReadOnly = true;
            mProperties.AddRange(source);
        }


        public IEnumerator<IDynamicEntityProperty> GetEnumerator() => mProperties.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => mProperties.GetEnumerator();

        public void Add(IDynamicEntityProperty item)
        {
            if (IsReadOnly)
                throw new InvalidOperationException(READONLY);
            mProperties.Add(item);
        }

        public void Clear()
        {
            if (IsReadOnly)
                throw new InvalidOperationException(READONLY);
            mProperties.Clear();

        }

        public bool Contains(IDynamicEntityProperty item) => IndexOf(item) >= 0;

        public void CopyTo(IDynamicEntityProperty[] array, int arrayIndex)
        {
            mProperties.CopyTo(array, arrayIndex);
        }

        public bool Remove(IDynamicEntityProperty item)
        {
            if (IsReadOnly)
                throw new InvalidOperationException(READONLY);
            int index = IndexOf(item);
            if (index < 0)
                return false;
            mProperties.RemoveAt(index);
            return true;
        }

        public int Count => mProperties.Count;
        
        public int IndexOf(IDynamicEntityProperty item)
        {
            for (int i = 0; i < mProperties.Count; i++)
                if (object.ReferenceEquals(item, mProperties[i]))
                    return i;
            return -1;
        }

        public void Insert(int index, IDynamicEntityProperty item)
        {
            if (IsReadOnly)
                throw new InvalidOperationException(READONLY);
            mProperties.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            if (IsReadOnly)
                throw new InvalidOperationException(READONLY);
            mProperties.RemoveAt(index);
        }

        public IDynamicEntityProperty this[int index]
        {
            get => mProperties[index];
            set
            {
                if (IsReadOnly)
                    throw new InvalidOperationException(READONLY);
                mProperties[index] = value;
            }
        }
    }
}