using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    internal class EntityQueryBuilder : AQueryBuilder
    {
        protected EntityDescriptor mEntityDescriptor;

        public EntityDescriptor Descriptor => mEntityDescriptor;

        protected virtual bool ExecuteNoData => true;
        protected AQueryBuilder mQueryBuilder;

        public AQueryBuilder QueryBuilder => mQueryBuilder;

        protected EntityQueryBuilder(SqlDbLanguageSpecifics languageSpecifics, Type type) : base(languageSpecifics)
        {
            mEntityDescriptor = AllEntities.Inst[type];
            mQueryBuilder = null;
        }

        internal EntityQueryBuilder(SqlDbLanguageSpecifics languageSpecifics, Type type, AQueryBuilder queryBuilder) : this(languageSpecifics, type)
        {
            mQueryBuilder = queryBuilder;
        }

        public override void PrepareQuery() => mQueryBuilder.PrepareQuery();
        public override string Query => mQueryBuilder.Query;
    }
}
