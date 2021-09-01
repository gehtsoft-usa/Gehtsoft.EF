using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The base class for dynamic entities.
    ///
    /// Note: dynamic entities will not be discovered by default by <see cref="EntityFinder"/>.
    /// In order to enable discovery them in finder, call <see cref="AllEntities.EnableDynamicEntityDiscoveryInEntityFinder"/> method.
    ///
    /// To implement a dynamic property, override
    /// <see cref="EntityAttribute"/> property and <see cref="DynamicEntity.InitializeProperties"/> method.
    /// </summary>
    public abstract class DynamicEntity : DynamicObject
    {
        /// <summary>
        /// Override the method to initialize the list of the properties.
        /// </summary>
        /// <returns></returns>
        protected abstract IEnumerable<IDynamicEntityProperty> InitializeProperties();
        private readonly DynamicEntityPropertyCollection mProperties;

        private class Container
        {
            internal IDynamicEntityProperty PropertyInfo { get; set; }
            internal object Value { get; set; }
        }

        /// <summary>
        /// Override the method to return the entity attribute.
        /// </summary>
        public abstract EntityAttribute EntityAttribute { get; }

        /// <summary>
        /// Override the method to return the obsolete entity attribute
        /// </summary>
        public virtual ObsoleteEntityAttribute ObsoleteEntityAttribute { get; } = null;

        private readonly Dictionary<string, Container> mValues = new Dictionary<string, Container>();

        /// <summary>
        /// Returns the list of the properties metadata.
        /// </summary>
        public IList<IDynamicEntityProperty> Properties => mProperties;

        protected DynamicEntity()
        {
#pragma warning disable S1699 // Constructors should only call non-overridable methods
            // as designed
            mProperties = new DynamicEntityPropertyCollection(InitializeProperties());
#pragma warning restore S1699 // Constructors should only call non-overridable methods
            foreach (IDynamicEntityProperty property in mProperties)
            {
                object value = null;
                if (property.PropertyType.IsValueType)
                    value = Activator.CreateInstance(property.PropertyType);
                mValues[property.Name] = new Container() { PropertyInfo = property, Value = value };
            }
        }

        /// <summary>
        /// Returns the names of the properties.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            List<string> names = new List<string>();
            names.AddRange(base.GetDynamicMemberNames());
            foreach (IDynamicEntityProperty property in mProperties)
                names.Add(property.Name);
            return names;
        }

        /// <summary>
        /// Reads a property.
        /// </summary>
        /// <param name="binder"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (mValues.TryGetValue(binder.Name, out Container container))
            {
                result = container.Value;
                return true;
            }
            else
                return base.TryGetMember(binder, out result);
        }

        /// <summary>
        /// Writes the property.
        /// </summary>
        /// <param name="binder"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            if (mValues.TryGetValue(binder.Name, out Container container))
            {
                container.Value = value;
                return true;
            }
            else
                return base.TrySetMember(binder, value);
        }
    }
}