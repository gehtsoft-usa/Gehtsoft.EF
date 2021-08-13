using System;
using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Gehtsoft.EF.Test.Utils
{
    public class TestOrderAttributeOrderer : ITestCaseOrderer
    {
        public const string CLASS = "Gehtsoft.EF.Test.Utils.TestOrderAttributeOrderer";
        public const string ASSEMBLY = "Gehtsoft.EF.Test";

        private static int GetOrder<TTestCase>(TTestCase testCase)
            where TTestCase : ITestCase
        {
            var attribute = testCase.TestMethod.Method.GetCustomAttributes(typeof(TestOrderAttribute).AssemblyQualifiedName).FirstOrDefault();
            if (attribute == null)
                return int.MaxValue;
            int x = (int)attribute.GetConstructorArguments().First();
            return x;
        }

        IEnumerable<TTestCase> ITestCaseOrderer.OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        {
            var cases = testCases.ToArray();
            Array.Sort(cases, (a, b) => GetOrder(a).CompareTo(GetOrder(b)));
            return cases;
        }
    }
}
