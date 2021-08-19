using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;

namespace Gehtsoft.EF.Test.Utils.DummyDb
{
    internal class DummyDbParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> mParameters = new List<DbParameter>();

        public override int Count => mParameters.Count;

        public override object SyncRoot { get; } = new object();

        public override int Add(object value)
        {
            if (value is DbParameter p)
                mParameters.Add(p);
            else
                throw new ArgumentException("Incorrect type", nameof(value));
            return mParameters.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (object o in values)
                Add(o);
        }

        public override void Clear()
        {
            mParameters.Clear();
        }

        public override bool Contains(object value) => IndexOf(value) >= 0;

        public override bool Contains(string value) => IndexOf(value) >= 0;

        public override void CopyTo(Array array, int index)
        {
            for (int i = 0; i < mParameters.Count; i++)
                array.SetValue(mParameters[i], i + index);
        }

        public override IEnumerator GetEnumerator() => mParameters.GetEnumerator();

        public override int IndexOf(object value)
        {
            if (value is DbParameter p)
                return IndexOf(p);
            else
                throw new ArgumentException("Incorrect type", nameof(value));
        }

        public override int IndexOf(string parameterName)
        {
            for (int i = 0; i < mParameters.Count; i++)
                if (mParameters[i].ParameterName == parameterName)
                    return i;
            return -1;
        }

        public override void Insert(int index, object value)
        {
            if (value is DbParameter p)
                mParameters.Insert(index, p);
            else
                throw new ArgumentException("Incorrect type", nameof(value));
        }

        public override void Remove(object value)
        {
            if (value is DbParameter p)
                mParameters.Remove(p);
            else
                throw new ArgumentException("Incorrect type", nameof(value));
        }

        public override void RemoveAt(int index)
        {
            mParameters.RemoveAt(index);
        }

        public override void RemoveAt(string parameterName)
        {
            for (int i = 0; i < mParameters.Count; i++)
                if (mParameters[i].ParameterName == parameterName)
                    mParameters.RemoveAt(i--);
        }

        protected override DbParameter GetParameter(int index)
        {
            return mParameters[index];
        }

        protected override DbParameter GetParameter(string parameterName)
        {
            for (int i = 0; i < mParameters.Count; i++)
                if (mParameters[i].ParameterName == parameterName)
                    return mParameters[i];
            return null;
        }

        protected override void SetParameter(int index, DbParameter value)
        {
            if (index >= 0 && index < mParameters.Count)
                mParameters[index] = value;
            else
                throw new ArgumentOutOfRangeException(nameof(index));
        }

        protected override void SetParameter(string parameterName, DbParameter value)
        {
            value.ParameterName = parameterName;
            for (int i = 0; i < mParameters.Count; i++)
                if (mParameters[i].ParameterName == parameterName)
                {
                    mParameters[i] = value;
                    return;
                }
            mParameters.Add(value);
        }
    }
}
