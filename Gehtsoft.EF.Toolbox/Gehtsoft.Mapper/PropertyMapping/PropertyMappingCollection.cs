using System;
using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.EF.Mapper
{
    public class PropertyMappingCollection : IPropertyMappingCollection
    {
        private List<IPropertyMapping> mMappings = new List<IPropertyMapping>();

        public void Add(IPropertyMapping mapping) => mMappings.Add(mapping);

        public int Count => mMappings.Count;

        public IPropertyMapping this[int index] => mMappings[index];

        IPropertyMapping IPropertyMappingCollection.this[int index] => mMappings[index];

        public int Find(IMappingTarget target)
        {
            for (int i = 0; i < mMappings.Count; i++)
                if (mMappings[i].Target.Equals(target))
                    return i;
            return -1;
        }

        IEnumerator<IPropertyMapping> IEnumerable<IPropertyMapping>.GetEnumerator() => mMappings.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => mMappings.GetEnumerator();
    }

    public class PropertyMappingCollection<TSource, TDestination> : IEnumerable<PropertyMapping<TSource, TDestination>>, IPropertyMappingCollection 
    {
        private List<PropertyMapping<TSource, TDestination>> mMappings = new List<PropertyMapping<TSource, TDestination>>();

        public void Add(PropertyMapping<TSource, TDestination> mapping) => mMappings.Add(mapping);

        public int Count => mMappings.Count;

        public PropertyMapping<TSource, TDestination> this[int index] => mMappings[index];

        IPropertyMapping IPropertyMappingCollection.this[int index] => mMappings[index];

        void IPropertyMappingCollection.Add(IPropertyMapping mapping)
        {
            if (!(mapping is PropertyMapping<TSource, TDestination>))
                throw new ArgumentException("Only property generalized property mappings may be added to a collection");
        }


        public int Find(IMappingTarget target)
        {
            for (int i = 0; i < mMappings.Count; i++)
                if (mMappings[i].Target.Equals(target))
                    return i;
            return -1;
        }

        public IEnumerator<PropertyMapping<TSource, TDestination>> GetEnumerator() => mMappings.GetEnumerator();

        IEnumerator<IPropertyMapping> IEnumerable<IPropertyMapping>.GetEnumerator() => mMappings.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}