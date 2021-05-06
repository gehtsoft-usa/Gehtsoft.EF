using System;
using System.Text.RegularExpressions;

namespace Gehtsoft.Validator
{
    public class DoesNotMatchPredicate : IValidationPredicate
    {
        private readonly Regex mRegex;
        private readonly string mPattern;

        public DoesNotMatchPredicate(Type parameterType, string pattern, RegexOptions? options = null, TimeSpan? timeout = null)
        {
            mParameterType = parameterType;
            mPattern = pattern;

            if (timeout != null)
                mRegex = new Regex(pattern, options ?? RegexOptions.None, (TimeSpan)timeout);
            else
                mRegex = new Regex(pattern, options ?? RegexOptions.None);
        }

        private readonly Type mParameterType;
        public Type ParameterType => mParameterType;

        public bool Validate(object value) => value != null && !mRegex.IsMatch(value?.ToString());

        public string RemoteScript(Type compilerType) => $"!jsv_match(/{mPattern}/{Suffix(mRegex.Options)}, value)";

        public string Suffix(RegexOptions options)
        {
            string s = "";
            if ((options & RegexOptions.IgnoreCase) != 0)
                s += "i";
            if ((options & RegexOptions.Multiline) != 0)
                s += "m";
            return s;
        }
    }
}