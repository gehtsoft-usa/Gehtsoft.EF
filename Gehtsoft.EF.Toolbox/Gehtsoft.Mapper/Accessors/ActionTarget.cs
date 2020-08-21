using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Mapper
{
    public class ActionTarget<TDestination, TType> : IMappingTarget
    {
        protected Action<TDestination, TType> Action { get; private set; }

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
