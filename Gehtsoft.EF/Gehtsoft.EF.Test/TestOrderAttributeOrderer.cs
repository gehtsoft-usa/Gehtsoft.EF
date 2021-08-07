using System.Collections.Generic;
using System.Linq;
using Xunit.Sdk;

namespace Gehtsoft.EF.Test
{
    public class TestOrderAttributeOrderer : ITestCaseOrderer
    {
        IEnumerable<TTestCase> ITestCaseOrderer.OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        {
            return testCases.OrderBy(testCase =>
            {
                var attribute = testCase.TestMethod.Method.GetCustomAttributes(typeof(TestOrderAttribute).AssemblyQualifiedName).FirstOrDefault();
                if (attribute == null)
                    return int.MaxValue;
                var x = (int)attribute.GetConstructorArguments().First();
                return x;
            });
        }
    }
}
