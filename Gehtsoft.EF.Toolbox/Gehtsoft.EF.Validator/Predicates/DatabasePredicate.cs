using System;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.Validator;

namespace Gehtsoft.EF.Validator
{
    public abstract class DatabasePredicate : IValidationPredicate, IValidationPredicateAsync
    {
        public IValidatorConnectionFactory ConnectionFactory { get; }
        public Type EntityType { get; }
        public TableDescriptor.ColumnInfo RelatedColumn { get; }

        protected DatabasePredicate(IValidatorConnectionFactory connectionFactory, Type entityType, TableDescriptor.ColumnInfo relatedColumn)
        {
            ConnectionFactory = connectionFactory;
            EntityType = entityType;
            RelatedColumn = relatedColumn;
        }

        public Type ParameterType => RelatedColumn.PropertyAccessor.PropertyType;

        public virtual bool Validate(object value)
        {
            SqlDbConnection connection = ConnectionFactory.GetConnection();
            try
            {
                return ValidateCore(true, connection, value, null).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            finally
            {
                if (ConnectionFactory.NeedToDispose)
                    connection.Dispose();
            }
        }

        public virtual async Task<bool> ValidateAsync(object value, CancellationToken? token = null)
        {
            SqlDbConnection connection = await ConnectionFactory.GetConnectionAsync(token);
            try
            {
                return await ValidateCore(false, connection, value, token);
            }
            finally
            {
                if (ConnectionFactory.NeedToDispose)
                    connection.Dispose();
            }
        }

        public string RemoteScript(Type compilerType) => null;

        protected abstract Task<bool> ValidateCore(bool sync, SqlDbConnection connection, object value, CancellationToken? token);
    }
}