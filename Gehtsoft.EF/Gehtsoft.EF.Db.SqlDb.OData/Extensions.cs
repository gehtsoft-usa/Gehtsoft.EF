using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.OData
{
    public static class Extensions
    {
        public static string EscapeXml(this string s)
        {
            string toxml = s;
            if (!string.IsNullOrEmpty(toxml))
            {
                // replace literal values with entities
                toxml = toxml.Replace("&", "&amp;");
                toxml = toxml.Replace("'", "&apos;");
                toxml = toxml.Replace("\"", "&quot;");
                toxml = toxml.Replace(">", "&gt;");
                toxml = toxml.Replace("<", "&lt;");
            }
            return toxml;
        }

        public static bool CanDelete(this SqlDbConnection connection, object entity) => connection.CanDeleteCore(true, entity, null).ConfigureAwait(false).GetAwaiter().GetResult();

        public static Task<bool> CanDeleteAsync<T>(this SqlDbConnection connection, T entity, CancellationToken? token)
        {
            return connection.CanDeleteCore(false, entity, token);
        }

        internal static async Task<bool> CanDeleteCore(this SqlDbConnection connection, bool sync, object entity, CancellationToken? token)
        {
            EntityDescriptor value = AllEntities.Inst[entity.GetType()];

            foreach (Type t in AllEntities.Inst)
            {
                EntityDescriptor other = AllEntities.Inst[t];
                foreach (TableDescriptor.ColumnInfo ci in other.TableDescriptor)
                {
                    if (ci.ForeignKey && ci.ForeignTable.Name == value.TableDescriptor.Name)
                    {
                        using (SelectEntitiesCountQuery query = connection.GetSelectEntitiesCountQuery(t))
                        {
                            query.Where.Add().Property(ci.ID).Is(CmpOp.Eq).Value(entity);
                            if (!sync)
                                await query.ExecuteAsync(token);
                            if (query.RowCount > 0)
                                return false;
                        }
                    }
                }
            }
            return true;
        }
    }
}
