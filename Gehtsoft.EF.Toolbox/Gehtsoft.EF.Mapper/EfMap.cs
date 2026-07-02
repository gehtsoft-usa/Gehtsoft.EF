using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Mapper
{
    public class EfMap<TSource, TDestination> : Map<TSource, TDestination>
    {
        protected readonly EntityDescriptor mDestinationDescriptor = null;

        public EfMap() : base()
        {
            Type destinationType = typeof(TDestination);
            EntityDescriptor entityInfo = AllEntities.Inst[destinationType, false];
            if (entityInfo != null)
                mDestinationDescriptor = entityInfo;
        }

        protected override IMappingTarget GetTargetByName(string name)
        {
            // When the destination is an entity, allow the lookup name to be a column ID or a DB
            // column name in addition to the CLR property name. Whatever it matches, resolve it to
            // the CLR property and let the base build the target, so the returned target is a
            // ClassPropertyAccessor that compares equal to the rules registered through For(...).
            if (mDestinationDescriptor != null)
            {
                for (int i = 0; i < mDestinationDescriptor.TableDescriptor.Count; i++)
                {
                    TableDescriptor.ColumnInfo columnInfo = mDestinationDescriptor.TableDescriptor[i];
                    if (columnInfo.ID == name || columnInfo.Name == name)
                        return base.GetTargetByName(columnInfo.PropertyAccessor.Name);
                }
            }

            return base.GetTargetByName(name);
        }
    }
}
