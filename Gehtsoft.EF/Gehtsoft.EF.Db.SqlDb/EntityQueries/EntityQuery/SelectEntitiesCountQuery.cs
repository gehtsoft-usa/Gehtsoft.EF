using System;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The class to select the count of the queries.
    ///
    /// Use <see cref="EntityConnectionExtension.GetSelectEntitiesCountQuery(SqlDbConnection, Type)"/>
    /// to get the instance of this object.
    ///
    /// The object instance must be disposed after use. Some databases requires the query to be disposed before the next query may be executed.
    /// </summary>
    public class SelectEntitiesCountQuery : SelectEntitiesQueryBase
    {
        internal SelectEntitiesCountQuery(SqlDbQuery query, SelectEntityQueryBuilderBase builder) : base(query, builder)
        {
        }

        protected SelectEntitiesCountQuery(Type type, SqlDbConnection connection) : this(connection.GetQuery(), new SelectEntityQueryBuilderBase(type, connection))
        {
        }

        protected int mCount = -1;

        /// <summary>
        /// The count of the rows that matches the condition.
        /// </summary>
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