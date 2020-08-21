using System;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class SelectEntitiesCountQuery : SelectEntitiesQueryBase
    {
        internal SelectEntitiesCountQuery(SqlDbQuery query, SelectEntityQueryBuilderBase builder) : base(query, builder)
        {
        }

        protected SelectEntitiesCountQuery(Type type, SqlDbConnection connection) : this(connection.GetQuery(), new SelectEntityQueryBuilderBase(type, connection))
        {
        }


        protected int mCount = -1;

        public virtual int RowCount
        {
            get
            {
                if (Executed)
                {
                    if (mCount >= 0)
                        return mCount;

                    mQuery.ReadNext();
                    mCount = mQuery.GetValue<int>(0);
                    return mCount;
                }
                else
                {
                    Execute();
                    mQuery.ReadNext();
                    mCount = mQuery.GetValue<int>(0);
                    return mCount;
                }
            }
        }
    }
}