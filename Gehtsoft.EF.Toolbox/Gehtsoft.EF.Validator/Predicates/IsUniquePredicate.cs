using System;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Validator
{
    public class IsUniquePredicate : DatabasePredicate
    {
        public IsUniquePredicate(IValidatorConnectionFactory connectionFactory, Type entityType, TableDescriptor.ColumnInfo relatedColumn) : base(connectionFactory, entityType, relatedColumn)
        {
        }

        protected override async Task<bool> ValidateCore(bool sync, SqlDbConnection connection, object value, CancellationToken? token)
        {
            using (SelectEntitiesCountQuery query = connection.GetSelectEntitiesCountQuery(EntityType))
            {
                object kv = RelatedColumn.PropertyAccessor.GetValue(value);

                if (kv == null)
                    query.Where.Property(RelatedColumn.PropertyAccessor.Name).Is(CmpOp.IsNull);
                else
                    query.Where.Property(RelatedColumn.PropertyAccessor.Name).Is(CmpOp.Eq).Value(kv);
                if (!RelatedColumn.PrimaryKey)
                    query.Where.Property(RelatedColumn.Table.PrimaryKey.PropertyAccessor.Name).Is(CmpOp.Neq).Value(RelatedColumn.Table.PrimaryKey.PropertyAccessor.GetValue(value));
                if (!sync)
                    await query.ExecuteAsync(token);
                return query.RowCount == 0;
            }
        }
    }

    public class ReferenceExistsPredicate : DatabasePredicate
    {
        public bool PkOnly { get; }

        public ReferenceExistsPredicate(IValidatorConnectionFactory connectionFactory, Type entityType, TableDescriptor.ColumnInfo relatedColumn, bool pkOnly = false) : base(connectionFactory, entityType, relatedColumn)
        {
            PkOnly = pkOnly;
        }

        protected override async Task<bool> ValidateCore(bool sync, SqlDbConnection connection, object value, CancellationToken? token)
        {
            using (SelectEntitiesCountQuery query = connection.GetSelectEntitiesCountQuery(RelatedColumn.PropertyAccessor.PropertyType))
            {
                query.Where.Property(RelatedColumn.ForeignTable.PrimaryKey.PropertyAccessor.Name).Is(CmpOp.Eq).Value(PkOnly ? value : RelatedColumn.ForeignTable.PrimaryKey.PropertyAccessor.GetValue(value));
                if (!sync)
                    await query.ExecuteAsync(token);
                return query.RowCount > 0;
            }
        }
    }
}