using System;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public abstract class AQueryBuilder
    {
        protected SqlDbLanguageSpecifics mSpecifics;

        protected AQueryBuilder(SqlDbLanguageSpecifics specifics)
        {
            mSpecifics = specifics;
        }

        public abstract void PrepareQuery();

        public abstract string Query { get; }

        public virtual IInQueryFieldReference GetReference(TableDescriptor.ColumnInfo column) => throw new EfSqlException(EfExceptionCode.FeatureNotSupported);
    }
}
