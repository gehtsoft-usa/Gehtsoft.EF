using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Validator;

namespace Gehtsoft.EF.Mapper.Validator
{
    public class IsModelUniquePredicate : IsUniquePredicate
    {
        public PropertyInfo PkInfo { get; private set; }
        public PropertyInfo ValueInfo { get; private set; }
       

        public IsModelUniquePredicate(PropertyInfo pk, PropertyInfo value, IValidatorConnectionFactory connectionFactory, Type entityType, TableDescriptor.ColumnInfo relatedColumn) : base(connectionFactory, entityType, relatedColumn)
        {
            PkInfo = pk;
            ValueInfo = value;

        }

        public override bool Validate(object value)
        {
            object entity = Activator.CreateInstance(EntityType);
            RelatedColumn.Table.PrimaryKey.PropertyAccessor.SetValue(entity, PkInfo.GetValue(value));
            RelatedColumn.PropertyAccessor.SetValue(entity, ValueInfo.GetValue(value));
            return base.Validate(entity);

        }
    }
}
