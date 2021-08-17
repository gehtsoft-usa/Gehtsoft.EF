using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Collections;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Gehtsoft.Tools.TypeUtils;

namespace Gehtsoft.EF.Test.Entity.Utils
{
    public static class ReferenceTypeAssertionExtension
    {
        private static bool Equals<T>(T e, T target)
        {
            if (e == null && target == null)
                return true;

            if (e == null || target == null)
                return false;

            if (ReferenceEquals(e, target))
                return true;

            if (e is Array x && target is Array y)
            {
                if (x.Length != y.Length)
                    return false;
                for (int i = 0; i < x.Length; i++)
                {
                    if (!Equals<object>(x.GetValue(i), y.GetValue(i)))
                        return false;
                }
                return true;
            }

            if (e is IEquatable<T> eq)
                return eq.Equals(target);

            if (e is IComparable<T> cmp1)
                return cmp1.CompareTo(target) == 0;

            if (e is IComparable cmp2)
                return cmp2.CompareTo(target) == 0;

            return false;
        }

        public static AndConstraint<TA> BeEqualTo<TS, TA>(this TA assertions, TS target, string because = null, params object[] becauseArgs)
            where TA : ReferenceTypeAssertions<TS, TA>
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => assertions.Subject)
                .ForCondition(e => Equals<TS>(e, target))
                .FailWith("Expected that the object is {0} but it is {1}", target, assertions.Subject);

            return new AndConstraint<TA>(assertions);
        }
    }

    public static class GenericCollectionAssertionExtension
    {
        public static AndConstraint<GenericCollectionAssertions<T>> HaveOneElementAfterTheOther<T>(this GenericCollectionAssertions<T> collection, T one, T two, string because = null, params object[] args)
        {
            Execute.Assertion
                .BecauseOf(because, args)
                .Given(() => collection.Subject)
                .ForCondition(e =>
                {
                    bool f1 = false, f2 = false;
                    e.ForEach(t =>
                    {
                        if (ReferenceEquals(t, one) || t.Equals(one))
                            f1 = true;
                        if ((ReferenceEquals(t, two) || t.Equals(two)) && f1)
                            f2 = true;
                    });
                    return f2;
                })
                .FailWith("Expected {context:the collection} contain {0} and then {1} but it does not", one, two);

            return new AndConstraint<GenericCollectionAssertions<T>>(collection);
        }

        public static AndConstraint<GenericCollectionAssertions<T>> HaveOneElementAfterTheOther<T>(this GenericCollectionAssertions<T> collection, Func<T, bool> one, Func<T, bool> two, string because = null, params object[] args)
        {
            Execute.Assertion
                .BecauseOf(because, args)
                .Given(() => collection.Subject)
                .ForCondition(e =>
                {
                    bool f1 = false, f2 = false;
                    e.ForEach(t =>
                    {
                        if (one(t))
                            f1 = true;
                        if (two(t) && f1)
                            f2 = true;
                    });
                    return f2;
                })
                .FailWith("Expected {context:the collection} contain {0} and then {1} but it does not", one, two);

            return new AndConstraint<GenericCollectionAssertions<T>>(collection);
        }
    }
}
