using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Gehtsoft.EF.Utils
{
    /// <summary>
    /// The tags collection.
    /// </summary>
    public class TagCollection : IEnumerable<KeyValuePair<object, object>>
    {
        private ConcurrentDictionary<object, object> mTags = null;

        private bool GetTag(object key, out object v)
        {
            v = null;
            if (mTags == null)
                return false;
            if (!mTags.TryGetValue(key, out v))
                return false;
            return true;
        }

        /// <summary>
        /// Gets a tag to the connection.
        ///
        /// A tag is any user-defined value. You can use tags to keep connection-related data.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="expectedType"></param>
        /// <returns></returns>
        public object GetTag(object key, Type expectedType = null)
        {
            if (!GetTag(key, out object v))
                v = null;
            return TypeConverter.Convert(v, expectedType ?? typeof(object), CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets a tag to the connection (generic method).
        ///
        /// A tag is any user-defined value. You can use tags to keep connection-related data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public T GetTag<T>(object key, T defaultValue = default)
        {
            if (!GetTag(key, out var v))
                return defaultValue;
            return (T)TypeConverter.Convert(v, typeof(T), CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Sets a tag to the connection.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SetTag(object key, object value)
        {
            if (mTags == null)
                mTags = new ConcurrentDictionary<object, object>();
            if (value == null)
                mTags.TryRemove(key, out _);
            else
                mTags.TryAdd(key, value);
        }

        /// <summary>
        /// Removes the tag if exists
        /// </summary>
        /// <param name="key"></param>
        public void Remove(object key)
        {
            mTags?.TryRemove(key, out _);
        }

        /// <summary>
        /// All tag keys
        /// </summary>
        public IEnumerable<object> Keys
        {
            get
            {
                if (mTags == null)
                    return Array.Empty<object>();
                return mTags.Keys;
            }
        }

        [DocgenIgnore]
        public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
        {
            if (mTags == null)
                return new List<KeyValuePair<object, object>>().GetEnumerator();

            return mTags.GetEnumerator();
        }

        [ExcludeFromCodeCoverage]
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
