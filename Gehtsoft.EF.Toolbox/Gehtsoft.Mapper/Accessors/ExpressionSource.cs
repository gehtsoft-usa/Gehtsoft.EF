using System;

namespace Gehtsoft.EF.Mapper
{
    public class ExpressionSource<TEntity, TValue> : IMappingSource
    {
        private Func<TEntity, TValue> mAction;

        public ExpressionSource(Func<TEntity, TValue> expression)
        {
            mAction = expression;
        }

        public string Name => "expression";
        public Type ValueType => typeof(TValue);
        public TValue Get(TEntity obj) => mAction.Invoke(obj);
        object IMappingSource.Get(object obj) => mAction.Invoke((TEntity)obj);
    }
}