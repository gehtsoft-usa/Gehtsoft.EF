using System;
using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// Collection of entities of the type specified.
    /// </summary>
    /// <typeparam name="T">The type of the entity</typeparam>
    public class EntityCollection<T> : IEntityCollection<T>
    {
        private readonly List<T> mList = new List<T>();

        /// <summary>
        /// Returns the flag indicating whether the collection is readonly
        /// </summary>
        public bool IsReadOnly { get; set; } = false;

        public IEnumerator<T> GetEnumerator()
        {
            return mList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mList).GetEnumerator();
        }

        /// <summary>
        /// <para>Removes element for the collection.</para>
        /// <para>The method generates no error if there is no such element</para>
        /// </summary>
        /// <param name="item">The item to be removed</param>
        /// <returns></returns>
        public bool Remove(T item)
        {
            int index = Find(item);
            if (index < 0)
                return false;
            RemoveAt(index);
            return true;
        }

        /// <summary>
        /// Returns number of elements in the collection
        /// </summary>
        public int Count
        {
            get { return mList.Count; }
        }

        public int Find(T entity, IEqualityComparer<T> comparer)
        {
            for (int i = 0; i < Count; i++)
            {
                if (comparer.Equals(mList[i], entity))
                    return i;
            }
            return -1;
        }

        public int Find(Func<T, bool> predicate)
        {
            for (int i = 0; i < Count; i++)
                if (predicate(mList[i]))
                    return i;
            return -1;
        }

        public bool Contains(T item)
        {
            return Find(item, COMPARER) >= 0;
        }

        public void CopyTo(T[] array, int arrayIndex) => mList.CopyTo(array, arrayIndex);

        public bool Contains(T entity, IEqualityComparer<T> comparer)
        {
            return Find(entity, comparer) >= 0;
        }

        private static readonly EntityEqualityComparer<T> COMPARER = new EntityEqualityComparer<T>();

        public int Find(T entity)
        {
            return Find(entity, COMPARER);
        }

        /// <summary>
        /// Returns element by its index
        /// </summary>
        /// <param name="index">The index of the element in the collection</param>
        /// <exception cref="IndexOutOfRangeException">The exception is thrown if the index is out of range</exception>
        /// <returns></returns>
        public T this[int index]
        {
            get { return mList[index]; }
            set
            {
                mList[index] = value;
                OnChange?.Invoke(this, index);
            }
        }

        public void Add(T item)
        {
            mList.Add(item);
            AfterInsert?.Invoke(this, this.Count - 1);
        }

        public void AddRange(IEnumerable<T> entities)
        {
            foreach (T t in entities)
                Add(t);
        }

        public int IndexOf(T item) => Find(item);

        public void Insert(int index, T item)
        {
            mList.Insert(index, item);
            AfterInsert?.Invoke(this, index);
        }

        public void RemoveAt(int index)
        {
            BeforeDelete?.Invoke(this, index);
            mList.RemoveAt(index);
        }

        public void Clear()
        {
            BeforeDelete?.Invoke(this, -1);
            mList.Clear();
        }

        /// <summary>
        /// The delegate to be called when a new item is inserted into <see cref="EntityCollection{T}" />
        /// </summary>
        /// <param name="sender">The collection</param>
        /// <param name="index">The index of the element</param>
        public delegate void AfterInsertDelegate(EntityCollection<T> sender, int index);

        public delegate void OnChangeDelegate(EntityCollection<T> sender, int index);

        public delegate void BeforeDeleteDelegate(EntityCollection<T> sender, int index);

        /// <summary>
        /// The event to be fired when a new item is inserted the collection
        /// </summary>
        public event AfterInsertDelegate AfterInsert;

        public event AfterInsertDelegate OnChange;

        public event BeforeDeleteDelegate BeforeDelete;

        public void RaiseOnChange(int index)
        {
            OnChange?.Invoke(this, index);
        }

        public T[] ToArray()
        {
            return mList.ToArray();
        }

        public EntityCollection<T> Clone()
        {
            EntityCollection<T> rv = new EntityCollection<T>();
            rv.mList.AddRange(mList);
            return rv;
        }

        public bool Equals(EntityCollection<T> other)
        {
            if (other == null)
                return false;
            if (other.Count != Count)
                return false;

            for (int i = 0; i < Count; i++)
            {
                bool rc;

                if (this[i] is IEquatable<T> eq)
                    rc = eq.Equals(other[i]);
                else
                    rc = EntityComparerHelper.Equals(this[i], other[i]);

                if (!rc)
                    return false;
            }

            return true;
        }
    }
}