using System;
using System.Text.RegularExpressions;

namespace Gehtsoft.Validator
{
    public class SQLInjectionPredicate : DoesNotMatchPredicate
    {
        private const string mRegularExpression = @"('(''|[^'])*')|(;)|(\b(ALTER|CREATE|DELETE|DROP|EXEC(UTE)?|INSERT( +INTO)?|MERGE|SELECT|UPDATE|UNION( +ALL)?)\b)";

        public SQLInjectionPredicate() : base(typeof(string), mRegularExpression, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2.0))
        {
        }
    }
}