using System;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// Decorates the accessor of an object-typed geometry property so the framework sees it as a
    /// <c>byte[]</c> (WKB) column: on write the CLR geometry object is serialized to plain OGC WKB, and
    /// on read the WKB is deserialized back to the object, via the globally registered
    /// <see cref="IGeometryCodec"/>. Presenting <see cref="PropertyType"/> as <c>byte[]</c> means no
    /// change is needed in the binders or the language specifics.
    ///
    /// It is used only when the property is an object type; a raw <c>byte[]</c> geometry property keeps
    /// its ordinary accessor.
    /// </summary>
    [DocgenIgnore]
    public sealed class GeometryPropertyAccessor : IPropertyAccessor
    {
        private readonly IPropertyAccessor mInner;
        private readonly int mSrid;

        /// <summary>Initializes a new instance of the <see cref="GeometryPropertyAccessor"/> class.</summary>
        /// <param name="inner">The real accessor of the property that holds the geometry object.</param>
        /// <param name="srid">The declared SRID, applied to geometries read from the database.</param>
        public GeometryPropertyAccessor(IPropertyAccessor inner, int srid)
        {
            mInner = inner;
            mSrid = srid;
        }

        public string Name => mInner.Name;

        public Type PropertyType => typeof(byte[]);

        public object GetValue(object thisObject)
        {
            object value = mInner.GetValue(thisObject);
            if (value == null)
                return null;
            return GeometryCodecs.Resolve().ToWkb(value, false);
        }

        public void SetValue(object thisObject, object value)
        {
            if (value == null)
            {
                mInner.SetValue(thisObject, null);
                return;
            }
            object geometry = GeometryCodecs.Resolve().FromWkb((byte[])value, mSrid);
            mInner.SetValue(thisObject, geometry);
        }

        public Attribute GetCustomAttribute(Type attributeType) => mInner.GetCustomAttribute(attributeType);

        public Attribute[] GetCustomAttributes(Type attributeType) => mInner.GetCustomAttributes(attributeType);
    }
}
