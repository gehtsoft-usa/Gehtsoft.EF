using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Mapper
{
    public sealed class ActionTarget<TDestination, TType> : IMappingTarget
    {
        private Action<TDestination, TType> Action { get; }

        internal ActionTarget(Action<TDestination, TType> action)
        {
            Action = action;
        }

        public bool Equals(IMappingTarget other)
        {
            return object.ReferenceEquals(this, other);
        }

        public string Name => "Action";
        public Type ValueType => typeof(TType);

        public void Set(object obj, object value)
        {
            if (obj == null)
                return;

            Action((TDestination)obj, (TType)value);
        }
    }
}
