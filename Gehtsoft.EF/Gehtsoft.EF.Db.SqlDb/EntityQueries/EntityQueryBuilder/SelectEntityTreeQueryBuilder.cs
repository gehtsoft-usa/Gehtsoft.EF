using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class SelectEntityTreeQueryBuilder : SelectEntityQueryBuilderBase
    {
        private static int mRootID;

        public string RootParameter { get; }

        public SelectEntityTreeQueryBuilder(Type type, SqlDbConnection connection, bool isRooted) : base(type, connection)
        {
            if (mEntityDescriptor.SelfReference == null)
                throw new ArgumentException("Entity type is not self-referenced", nameof(type));
            RootParameter = isRooted ? $"treeRoot{mRootID++}" : null;
            var mHierarchicalSelectQueryBuilder = connection.GetHierarchicalSelectQueryBuilder(mEntityDescriptor.TableDescriptor, mEntityDescriptor.SelfReference, RootParameter);
            mHierarchicalSelectQueryBuilder.IdOnlyMode = true;
            mHierarchicalSelectQueryBuilder.PrepareQuery();
            mSelectQueryBuilder.Where.And().Property(mEntityDescriptor.TableDescriptor.PrimaryKey).Is(CmpOp.In).Query(mHierarchicalSelectQueryBuilder);
        }
    }
}