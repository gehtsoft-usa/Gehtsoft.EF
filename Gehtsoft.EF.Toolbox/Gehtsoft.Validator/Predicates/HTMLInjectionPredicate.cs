using System;
using System.Text.RegularExpressions;

namespace Gehtsoft.Validator
{
    public class HTMLInjectionPredicate : DoesNotMatchPredicate
    {
        private const string mRegularExpression = @"[\<\>]";

        public HTMLInjectionPredicate() : base(typeof(string), mRegularExpression, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2.0))
        {
        }
    }
}