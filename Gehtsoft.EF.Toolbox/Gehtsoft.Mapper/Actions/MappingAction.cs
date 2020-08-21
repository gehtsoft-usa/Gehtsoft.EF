using System;

namespace Gehtsoft.EF.Mapper
{
    public class MappingAction<TSource, TTarget> : IMappingAction
    {
        private Action<TSource, TTarget> mAction;
        private Func<TSource, TTarget, bool> mPredicate;

        public MappingAction(Action<TSource, TTarget> action)
        {
            mAction = action;
            mPredicate = null;
        }

        public MappingAction(Action<TSource, TTarget> action, Func<TSource, TTarget, bool> predicate)
        {
            mAction = action;
            mPredicate = predicate;
        }

        public void Perform(object source, object target)
        {
            if (mPredicate != null)
                if (!mPredicate((TSource) source, (TTarget) target))
                    return;
            mAction.Invoke((TSource) source, (TTarget) target);
        }

        public MappingAction<TSource, TTarget> When(Func<TSource, TTarget, bool> predicate)
        {
            mPredicate = predicate;
            return this;
        }

        public MappingAction<TSource, TTarget> Unless(Func<TSource, TTarget, bool> predicate)
        {
            mPredicate = (s, t) => !predicate(s, t);
            return this;
        }

        public MappingAction<TSource, TTarget> WhenNull() => When((s, t) => s == null);
        
        public MappingAction<TSource, TTarget> WhenNotNull() => When((s, t) => s != null);
    }
}