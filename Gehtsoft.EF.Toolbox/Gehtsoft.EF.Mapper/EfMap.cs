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
            IMappingTarget target = null;

            if (mDestinationDescriptor != null)
            {
                TableDescriptor.ColumnInfo columnInfo = null;

                for (int i = 0; i < mDestinationDescriptor.TableDescriptor.Count; i++)
                    if (mDestinationDescriptor.TableDescriptor[i].ID == name || mDestinationDescriptor.TableDescriptor[i].Name == name)
                        columnInfo = mDestinationDescriptor.TableDescriptor[i];

                if (columnInfo != null)
                    target = new EntityPropertyAccessor(columnInfo);
            }

            if (target == null)
                return base.GetTargetByName(name);
            else
                return target;
        }
    }
}
