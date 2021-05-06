using System;

namespace Gehtsoft.EF.Mapper
{
    public class MappingPredicate<TEntity> : IMappingPredicate
    {
        private readonly Func<TEntity, bool> mPredicate;

        public Type ParameterType => typeof(TEntity);

        public MappingPredicate(Func<TEntity, bool> predicate)
        {
            mPredicate = predicate;
        }
        public bool Evaluate(object obj)
        {
            return mPredicate((TEntity)obj);
        }
    }

    public class NotMappingPredicate : IMappingPredicate
    {
        private readonly IMappingPredicate mPredicate;

        public Type ParameterType => mPredicate.ParameterType;

        public NotMappingPredicate(IMappingPredicate predicate)
        {
            mPredicate = predicate;
        }

        public bool Evaluate(object obj)
        {
            return !mPredicate.Evaluate(obj);
        }
    }
}