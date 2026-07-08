using System;
using System.Text.Json;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// Decorates a property accessor of a JSON column so that the CLR value (a primitive, an array
    /// of primitives, or a <c>System.Text.Json</c>-annotated object) is serialized to a JSON string
    /// on write and deserialized on read.
    ///
    /// It presents <see cref="PropertyType"/> as <see cref="string"/> to the value pipeline, so the
    /// column behaves as an ordinary string column for binding and reading; no change is needed in
    /// the binders or the language specifics. It belongs to the SQL-builder layer so it can be used
    /// on a raw <see cref="TableDescriptor"/> without the entity layer.
    /// </summary>
    [DocgenIgnore]
    public sealed class JsonPropertyAccessor : IPropertyAccessor
    {
        private readonly IPropertyAccessor mInner;
        private readonly Type mClrType;
        private readonly JsonSerializerOptions mOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonPropertyAccessor"/> class.
        /// </summary>
        /// <param name="inner">The real accessor of the property that holds the CLR value.</param>
        /// <param name="options">Optional serializer options.</param>
        public JsonPropertyAccessor(IPropertyAccessor inner, JsonSerializerOptions options = null)
        {
            mInner = inner;
            mClrType = inner.PropertyType;
            mOptions = options;
        }

        public string Name => mInner.Name;

        public Type PropertyType => typeof(string);

        public object GetValue(object thisObject)
        {
            object value = mInner.GetValue(thisObject);
            if (value == null)
                return null;
            return JsonSerializer.Serialize(value, mClrType, mOptions);
        }

        public void SetValue(object thisObject, object value)
        {
            if (value == null)
            {
                mInner.SetValue(thisObject, null);
                return;
            }
            string json = value as string ?? value.ToString();
            object deserialized = JsonSerializer.Deserialize(json, mClrType, mOptions);
            mInner.SetValue(thisObject, deserialized);
        }

        public Attribute GetCustomAttribute(Type attributeType) => mInner.GetCustomAttribute(attributeType);

        public Attribute[] GetCustomAttributes(Type attributeType) => mInner.GetCustomAttributes(attributeType);
    }
}
