using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.Validator;

namespace Gehtsoft.EF.Validator
{
    public class EntityPropertyTarget : ValidationTarget
    {
        private readonly IPropertyAccessor mPropertyAccessor;

        public override string TargetName => mPropertyAccessor.Name;
        public override T GetCustomAttribute<T>() => mPropertyAccessor.GetCustomAttribute<T>();
        public override bool IsProperty => true;
        public override string PropertyName => mPropertyAccessor.Name;

        public EntityPropertyTarget(IPropertyAccessor accessor)
        {
            mPropertyAccessor = accessor;
        }

        public override Type ValueType => mPropertyAccessor.PropertyType;

        public override bool IsSingleValue => true;

        public override ValidationValue First(object target)
        {
            return new ValidationValue() { Name = mPropertyAccessor.Name, Value = mPropertyAccessor.GetValue(target) };
        }

        public override ValidationValue[] All(object target)
        {
            return new ValidationValue[] { First(target) };
        }
    }
}
