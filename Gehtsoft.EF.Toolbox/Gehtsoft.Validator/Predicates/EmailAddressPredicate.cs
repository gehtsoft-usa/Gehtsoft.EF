using System;
using System.Text.RegularExpressions;

namespace Gehtsoft.Validator
{
    public class EmailAddressPredicate : DoesMatchPredicate
    {
        private const string mEmailAddressRegex = @"^(([^<>()\[\]\.,;:\s@\x22]+(\.[^<>()\[\]\.,;:\s@\x22]+)*)|(\x22.+\x22))@(([^<>()[\]\.,;:\s@\x22]+\.)+[^<>()[\]\.,;:\s@\x22]{2,})$";

        public EmailAddressPredicate() : base(typeof(string), mEmailAddressRegex, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2.0))
        {
        }
    }
}