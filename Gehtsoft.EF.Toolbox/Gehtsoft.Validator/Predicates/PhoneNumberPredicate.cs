using System;
using System.Text.RegularExpressions;

namespace Gehtsoft.Validator
{
    public class PhoneNumberPredicate : DoesMatchPredicate
    {
        private const string PhoneRegex = @"^(\+\d{1,2}\s?)?\(?\d{3}\)?[\s.-]?\d{3}[\s.-]?\d{4}$";

        public PhoneNumberPredicate() : base(typeof(string), PhoneRegex, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2.0))
        {
        }
    }
}