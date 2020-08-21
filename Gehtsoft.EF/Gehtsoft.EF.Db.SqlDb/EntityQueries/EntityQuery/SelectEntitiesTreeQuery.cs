using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class SelectEntitiesTreeQuery : SelectEntitiesQueryBase
    {
        internal SelectEntityTreeQueryBuilder TreeQueryBuilder { get; private set; }

        internal SelectEntitiesTreeQuery(SqlDbQuery query, SelectEntityTreeQueryBuilder builder) : base(query, builder)
        {
            TreeQueryBuilder = builder;
        }

        public object Root
        {
            set
            {
                if (TreeQueryBuilder.RootParameter == null)
                    throw new InvalidOperationException("The query does not have a root parameter");
                if (value != null)
                {
                    if (value.GetType() != TreeQueryBuilder.Descriptor.TableDescriptor.PrimaryKey.PropertyAccessor.PropertyType)
                    {
                        EntityDescriptor descriptor = AllEntities.Inst[value.GetType(), false];
                        if (descriptor != null)
                            value = descriptor.TableDescriptor.PrimaryKey.PropertyAccessor.GetValue(value);
                        else
                            value = Convert.ChangeType(value, TreeQueryBuilder.Descriptor.TableDescriptor.PrimaryKey.PropertyAccessor.PropertyType);
                    }
                }
                mQuery.BindParam(TreeQueryBuilder.RootParameter, ParameterDirection.Input, value, TreeQueryBuilder.Descriptor.TableDescriptor.PrimaryKey.PropertyAccessor.PropertyType);
            }
        }
    }


}
