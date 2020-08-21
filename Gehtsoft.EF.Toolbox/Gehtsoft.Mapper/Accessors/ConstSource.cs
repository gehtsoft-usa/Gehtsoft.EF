using System;

namespace Gehtsoft.EF.Mapper
{
    public class ConstSource<TType> : IMappingSource
    {
        public ConstSource(TType value)
        {
            mConst = value;
        }

        public string Name => "const";
        public Type ValueType => typeof(TType);
        public object mConst;
        
        public object Get(object obj)
        {
            return mConst;
        }
    }
}