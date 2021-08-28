using System;
using System.Collections;
using System.Collections.Generic;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// The type of the change event
    /// </summary>
    public enum EntityCollectionEvent
    {
        Add,
        Update,
        Delete
    }

    /// <summary>
    /// The collection change event arguments
    /// </summary>
    public class EntityCollectionEventArgs<T>
    {
        public EntityCollection<T> Collection { get; }
        public int Index { get; }
        public EntityCollectionEvent Event { get; }
        internal EntityCollectionEventArgs(EntityCollectionEvent ev, EntityCollection<T> c, int index)
        {
            Index = index;
            Collection = c;
            Event = ev;
        }
    }

#pragma warning disable S3897 // Classes that provide "Equals(<T>)" should implement "IEquatable<T>"
#pragma warning disable S4035 // Classes implementing "IEquatable<T>" should be sealed
    /// <summary>
    /// Collection of entities of the type specified.
    /// </summary>
    /// <typeparam name="T">The type of the entity</typeparam>
    public class EntityCollection<T> : IEntityCollection<T>
    {
        private readonly List<T> mList = new List<T>();

        /// <summary>
        /// Returns the flag indicating whether the collection is read-only
        /// </summary>
        [DocgenIgnore]
        public bool IsReadOnly { get; set; } = false;

        /// <summary>
        /// Returns enumeration of the objects.
        /// </summary>
        /// <returns></returns>
        [DocgenIgnore]
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

        /// <summary>
        /// Finds an element using the specified equality comparer.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="comparer"></param>
        /// <returns>The index of the element or `-1` if the element is not found</returns>
        public int Find(T entity, IEqualityComparer<T> comparer)
        {
            for (int i = 0; i < Count; i++)
            {
                if (comparer.Equals(mList[i], entity))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Finds an element using the specified predicate.
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns>The index of the element or `-1` if the element is not found</returns>
        public int Find(Func<T, bool> predicate)
        {
            for (int i = 0; i < Count; i++)
                if (predicate(mList[i]))
                    return i;
            return -1;
        }

        /// <summary>
        /// Checks whether the collection contains the element.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(T item)
        {
            return Find(item, COMPARER) >= 0;
        }

        /// <summary>
        /// Copies the collection content to the array.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        public void CopyTo(T[] array, int arrayIndex) => mList.CopyTo(array, arrayIndex);

        /// <summary>
        /// Checks whether the collection contains the element using the comparer specified.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="comparer"></param>
        /// <returns></returns>
        public bool Contains(T entity, IEqualityComparer<T> comparer)
        {
            return Find(entity, comparer) >= 0;
        }

        private static readonly EntityEqualityComparer<T> COMPARER = new EntityEqualityComparer<T>();

        /// <summary>
        /// Finds an element using a default comparer.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
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
                OnChange?.Invoke(this, new EntityCollectionEventArgs<T>(EntityCollectionEvent.Update, this, index));
            }
        }

        /// <summary>
        /// Adds an element to the of the collection.
        /// </summary>
        /// <param name="item"></param>
        public void Add(T item)
        {
            mList.Add(item);
            AfterInsert?.Invoke(this, new EntityCollectionEventArgs<T>(EntityCollectionEvent.Add, this, this.Count - 1));
        }

        /// <summary>
        /// Adds elements to the end of the collection.
        /// </summary>
        /// <param name="entities"></param>
        public void AddRange(IEnumerable<T> entities)
        {
            foreach (T t in entities)
                Add(t);
        }

        /// <summary>
        /// Returns index of the element.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public int IndexOf(T item) => Find(item);

        /// <summary>
        /// Inserts the element at the specified position.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        public void Insert(int index, T item)
        {
            mList.Insert(index, item);
            AfterInsert?.Invoke(this, new EntityCollectionEventArgs<T>(EntityCollectionEvent.Add, this, index));
        }

        /// <summary>
        /// Removes the element at the specified position.
        /// </summary>
        /// <param name="index"></param>
        public void RemoveAt(int index)
        {
            BeforeDelete?.Invoke(this, new EntityCollectionEventArgs<T>(EntityCollectionEvent.Delete, this, index));
            mList.RemoveAt(index);
        }

        /// <summary>
        /// Remove all elements from the collection.
        /// </summary>
        public void Clear()
        {
            BeforeDelete?.Invoke(this, new EntityCollectionEventArgs<T>(EntityCollectionEvent.Delete, this, -1));
            mList.Clear();
        }

        /// <summary>
        /// The event to be fired when a new item is inserted the collection
        /// </summary>
        public event EventHandler<EntityCollectionEventArgs<T>> AfterInsert;

        /// <summary>
        /// The event to be fired when an item is replaced.
        /// </summary>
        public event EventHandler<EntityCollectionEventArgs<T>> OnChange;

        /// <summary>
        /// The event to be fired when the item is removed.
        /// </summary>
        public event EventHandler<EntityCollectionEventArgs<T>> BeforeDelete;

        internal void RaiseOnChange(int index)
        {
            OnChange?.Invoke(this, new EntityCollectionEventArgs<T>(EntityCollectionEvent.Update, this, index));
        }

        /// <summary>
        /// Converts the collection to an array.
        /// </summary>
        /// <returns></returns>
        public T[] ToArray()
        {
            return mList.ToArray();
        }

        /// <summary>
        /// Clones the collection.
        /// </summary>
        /// <returns></returns>
        public EntityCollection<T> Clone()
        {
            EntityCollection<T> rv = new EntityCollection<T>();
            rv.mList.AddRange(mList);
            return rv;
        }

        /// <summary>
        /// Checks whether two collections contains the equal set of entities
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        [DocgenIgnore]
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