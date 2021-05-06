using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.EF.Mapper
{
    public interface IMappingActionCollection : IEnumerable<IMappingAction>
    {
        int Count { get; }
        IMappingAction this[int index] { get; }
        void Add(IMappingAction action);
    }

    public class MappingActionCollection<TSource, TTarget> : IEnumerable<MappingAction<TSource, TTarget>>, IMappingActionCollection
    {
        private readonly List<MappingAction<TSource, TTarget>> mActions = new List<MappingAction<TSource, TTarget>>();

        public int Count => mActions.Count;

        public MappingAction<TSource, TTarget> this[int index] => mActions[index];

        IMappingAction IMappingActionCollection.this[int index] => mActions[index];

        public void Add(MappingAction<TSource, TTarget> action) => mActions.Add(action);

        void IMappingActionCollection.Add(IMappingAction action)
        {
            if (action is MappingAction<TSource, TTarget> a)
                Add(a);
        }

        IEnumerator<IMappingAction> IEnumerable<IMappingAction>.GetEnumerator() => mActions.GetEnumerator();

        public IEnumerator<MappingAction<TSource, TTarget>> GetEnumerator() => mActions.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}