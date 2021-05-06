using System;
using System.Text.RegularExpressions;

namespace Gehtsoft.Validator
{
    public class HtmlInjectionPredicate : DoesNotMatchPredicate
    {
        private const string mRegularExpression = @"[\<\>]";

        public HtmlInjectionPredicate() : base(typeof(string), mRegularExpression, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2.0))
        {
        }
    }
}