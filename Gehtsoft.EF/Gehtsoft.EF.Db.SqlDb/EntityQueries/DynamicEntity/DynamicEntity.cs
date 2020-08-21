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

        class Container
        {
            internal IDynamicEntityProperty PropertyInfo { get; set; }
            internal object Value { get; set; }
        }

        public abstract EntityAttribute EntityAttribute { get; }

        public virtual ObsoleteEntityAttribute ObsoleteEntityAttribute { get; } = null;

        private Dictionary<string, Container> mValues = new Dictionary<string, Container>();

        public IList<IDynamicEntityProperty> Properties => mProperties;

        protected DynamicEntity()
        {
            mProperties = new DynamicEntityPropertyCollection(InitializeProperties());
            foreach (IDynamicEntityProperty property in mProperties)
                mValues[property.Name] = new Container() {PropertyInfo = property, Value = null};
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