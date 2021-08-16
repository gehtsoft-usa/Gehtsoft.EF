﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public abstract class DynamicEntity : DynamicObject
    {
        protected abstract IEnumerable<IDynamicEntityProperty> InitializeProperties();
        private readonly DynamicEntityPropertyCollection mProperties;

        private class Container
        {
            internal IDynamicEntityProperty PropertyInfo { get; set; }
            internal object Value { get; set; }
        }

        public abstract EntityAttribute EntityAttribute { get; }

        public virtual ObsoleteEntityAttribute ObsoleteEntityAttribute { get; } = null;

        private readonly Dictionary<string, Container> mValues = new Dictionary<string, Container>();

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

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            List<string> names = new List<string>();
            names.AddRange(base.GetDynamicMemberNames());
            foreach (IDynamicEntityProperty property in mProperties)
                names.Add(property.Name);
            return names;
        }

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