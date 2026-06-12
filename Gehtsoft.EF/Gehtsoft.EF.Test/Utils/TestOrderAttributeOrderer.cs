using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit.Sdk;
using Xunit.v3;

namespace Gehtsoft.EF.Test.Utils
{
    public class TestOrderAttributeOrderer : ITestCaseOrderer
    {
        private static int GetOrder(ITestCase testCase)
        {
            var attribute = (testCase as IXunitTestCase)?.TestMethod.Method
                .GetCustomAttribute<TestOrderAttribute>();
            return attribute?.Order ?? int.MaxValue;
        }

        public IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> testCases)
            where TTestCase : notnull, ITestCase
        {
            var cases = testCases.ToArray();
            Array.Sort(cases, (a, b) => GetOrder(a).CompareTo(GetOrder(b)));
            return cases;
        }
    }
}
