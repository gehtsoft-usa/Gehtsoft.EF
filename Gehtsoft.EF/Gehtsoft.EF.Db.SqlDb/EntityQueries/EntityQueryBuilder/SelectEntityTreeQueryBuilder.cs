using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class SelectEntityTreeQueryBuilder : SelectEntityQueryBuilderBase
    {
        private HierarchicalSelectQueryBuilder mHierarchicalSelectQueryBuilder;
        private static int mRootID;
        private string mRootParameter;

        public string RootParameter => mRootParameter;

        public SelectEntityTreeQueryBuilder(Type type, SqlDbConnection connection, bool isRooted) : base(type, connection)
        {
            if (mEntityDescriptor.SelfReference == null)
                throw new ArgumentException("Entity type is not self-referenced", nameof(type));
            mHierarchicalSelectQueryBuilder = connection.GetHierarchicalSelectQueryBuilder(mEntityDescriptor.TableDescriptor, mEntityDescriptor.SelfReference, mRootParameter = (isRooted ? $"treeRoot{mRootID++}" : null));
            mHierarchicalSelectQueryBuilder.IdOnlyMode = true;
            mHierarchicalSelectQueryBuilder.PrepareQuery();
            mSelectQueryBuilder.Where.And().Property(mEntityDescriptor.TableDescriptor.PrimaryKey).Is(CmpOp.In).Query(mHierarchicalSelectQueryBuilder);
        }
    }
}