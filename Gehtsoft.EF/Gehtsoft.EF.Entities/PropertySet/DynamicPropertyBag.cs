using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// An in-memory bag of dynamic properties (a `name` to value map) with change tracking.
    ///
    /// The bag is driver-neutral: it does not know how values are stored - that is a back-end
    /// concern. It does, however, fix the supported-value-type contract: a property value must be
    /// one of the six primitive types <see cref="bool"/>, <see cref="int"/>, <see cref="long"/>,
    /// <see cref="double"/>, <see cref="string"/> or <see cref="System.DateTime"/> (or their
    /// <see cref="System.Nullable{T}"/> forms). Any other type is rejected.
    ///
    /// Changes are tracked eagerly against the baseline set by <see cref="Initialize"/> /
    /// <see cref="AcceptChanges"/>: each <see cref="Set"/> / <see cref="Remove"/> reconciles a
    /// single name, so <see cref="Added"/> / <see cref="Changed"/> / <see cref="Removed"/> cost is
    /// proportional to the number of changes (not the bag size) and <see cref="AnyModified"/> is O(1).
    /// A value that is changed back to its baseline (or added then removed) leaves no trace.
    /// </summary>
    public sealed class DynamicPropertyBag : IEnumerable<(string Name, object Value)>
    {
        // sentinel meaning "this name was absent at the baseline"
        private static readonly object Absent = new object();

        private readonly Dictionary<string, object> mValues = new Dictionary<string, object>(StringComparer.Ordinal);

        // net-changed names since the baseline -> their baseline value (or Absent). Names whose
        // current value equals the baseline are NOT present here.
        private readonly Dictionary<string, object> mOriginal = new Dictionary<string, object>(StringComparer.Ordinal);

        /// <summary>
        /// Creates an empty bag.
        /// </summary>
        public DynamicPropertyBag()
        {
        }

        /// <summary>
        /// Creates a bag pre-populated with the initial (untracked) state.
        /// </summary>
        /// <param name="initial">The initial properties.</param>
        public DynamicPropertyBag(IEnumerable<(string Name, object Value)> initial)
        {
            Initialize(initial);
        }

        /// <summary>
        /// Checks whether the specified CLR type is a supported dynamic property value type.
        ///
        /// The <see cref="System.Nullable{T}"/> form of a supported type is also supported
        /// (e.g. `int?` is supported because `int` is).
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>`true` if the type (or its underlying type) is one of the six supported primitives.</returns>
        public static bool IsSupportedType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            Type actual = Nullable.GetUnderlyingType(type) ?? type;
            return actual == typeof(bool)
                || actual == typeof(int)
                || actual == typeof(long)
                || actual == typeof(double)
                || actual == typeof(string)
                || actual == typeof(DateTime);
        }

        /// <summary>
        /// Whether this bag was created for a brand-new (not-yet-inserted) entity.
        ///
        /// A "new" bag has no persisted baseline and is only valid to use in an insert; a driver
        /// is expected to reject it anywhere else. The flag is cleared once the bag becomes a
        /// persisted / loaded baseline (see <see cref="AcceptChanges"/> / <see cref="Initialize"/>).
        /// </summary>
        public bool IsNew { get; internal set; }

        /// <summary>
        /// The number of properties currently in the bag.
        /// </summary>
        public int Count => mValues.Count;

        /// <summary>
        /// Checks whether a property with the specified name is present.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool Contains(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            return mValues.ContainsKey(name);
        }

        /// <summary>
        /// Sets a property value.
        ///
        /// A `null` value removes the property (a `null` is never stored; absence is equivalent to
        /// `null`). A non-null value must be of a supported type (see <see cref="IsSupportedType"/>).
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The value, or `null` to remove.</param>
        /// <exception cref="ArgumentException">The value is of an unsupported type.</exception>
        public void Set(string name, object value)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (value == null)
            {
                Remove(name);
                return;
            }

            if (!IsSupportedType(value.GetType()))
                throw new ArgumentException($"The type '{value.GetType().FullName}' is not a supported dynamic property value type", nameof(value));

            object before = mValues.TryGetValue(name, out object current) ? current : Absent;
            mValues[name] = value;
            Reconcile(name, before, value);
        }

        /// <summary>
        /// Removes a property.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <returns>`true` if the property was present and removed; otherwise `false`.</returns>
        public bool Remove(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (!mValues.TryGetValue(name, out object before))
                return false;

            mValues.Remove(name);
            Reconcile(name, before, Absent);
            return true;
        }

        // Reconciles the change tracking for a single name after a mutation.
        // before = the value prior to this mutation (or Absent); now = the value after (or Absent).
        private void Reconcile(string name, object before, object now)
        {
            if (mOriginal.TryGetValue(name, out object original))
            {
                // already diverged from the baseline since the last accept; the baseline value is
                // the stored 'original'. If we are back to it, the name is no longer a change.
                if (ValueEquals(original, now))
                    mOriginal.Remove(name);
            }
            else
            {
                // first touch since the baseline: 'before' IS the baseline value.
                if (!ValueEquals(before, now))
                    mOriginal[name] = before;
            }
        }

        private static bool ValueEquals(object a, object b)
        {
            if (ReferenceEquals(a, Absent) || ReferenceEquals(b, Absent))
                return ReferenceEquals(a, b);
            return Equals(a, b);
        }

        /// <summary>
        /// Gets a property value, or `null` if the property is absent.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <returns>The value, or `null`.</returns>
        public object Get(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            return mValues.TryGetValue(name, out object value) ? value : null;
        }

        /// <summary>
        /// Gets a property value converted to the specified type, or `default(T)` if absent.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="name">The property name.</param>
        /// <returns>The converted value, or `default(T)`.</returns>
        public T Get<T>(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (!mValues.TryGetValue(name, out object value) || value == null)
                return default;
            if (value is T typed)
                return typed;
            Type target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            return (T)Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Replaces the bag contents with the specified initial state and takes it as the change
        /// tracking baseline (nothing is reported as modified afterwards).
        /// </summary>
        /// <param name="properties">The initial properties. Each value must be of a supported type.</param>
        public void Initialize(IEnumerable<(string Name, object Value)> properties)
        {
            if (properties == null)
                throw new ArgumentNullException(nameof(properties));

            mValues.Clear();
            mOriginal.Clear();
            IsNew = false;
            foreach ((string name, object value) in properties)
            {
                if (value == null)
                    continue;
                if (!IsSupportedType(value.GetType()))
                    throw new ArgumentException($"The type '{value.GetType().FullName}' is not a supported dynamic property value type", nameof(properties));
                mValues[name] = value;
            }
        }

        /// <summary>
        /// Promotes the current contents to the change tracking baseline, so that nothing is
        /// reported as added, changed or removed until the next modification.
        /// </summary>
        public void AcceptChanges()
        {
            mOriginal.Clear();
            IsNew = false;
        }

        /// <summary>
        /// The properties added since the baseline (present now, absent in the baseline).
        /// </summary>
        public IEnumerable<(string Name, object Value)> Added
        {
            get
            {
                List<(string Name, object Value)> result = new List<(string Name, object Value)>();
                foreach (KeyValuePair<string, object> entry in mOriginal)
                    if (ReferenceEquals(entry.Value, Absent))
                        result.Add((entry.Key, mValues[entry.Key]));
                return result;
            }
        }

        /// <summary>
        /// The properties changed since the baseline (present in both with a different value).
        /// </summary>
        public IEnumerable<(string Name, object Value)> Changed
        {
            get
            {
                List<(string Name, object Value)> result = new List<(string Name, object Value)>();
                foreach (KeyValuePair<string, object> entry in mOriginal)
                    if (!ReferenceEquals(entry.Value, Absent) && mValues.ContainsKey(entry.Key))
                        result.Add((entry.Key, mValues[entry.Key]));
                return result;
            }
        }

        /// <summary>
        /// The names of the properties removed since the baseline (present in the baseline, absent now).
        /// </summary>
        public IEnumerable<string> Removed
        {
            get
            {
                List<string> result = new List<string>();
                foreach (KeyValuePair<string, object> entry in mOriginal)
                    if (!ReferenceEquals(entry.Value, Absent) && !mValues.ContainsKey(entry.Key))
                        result.Add(entry.Key);
                return result;
            }
        }

        /// <summary>
        /// Whether anything has been added, changed or removed since the baseline.
        /// </summary>
        public bool AnyModified => mOriginal.Count > 0;

        /// <summary>
        /// Enumerates the current properties as name/value pairs.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<(string Name, object Value)> GetEnumerator()
        {
            foreach (KeyValuePair<string, object> entry in mValues)
                yield return (entry.Key, entry.Value);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
