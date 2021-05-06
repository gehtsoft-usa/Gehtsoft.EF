using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Gehtsoft.Validator
{
    public class DoesMatchPredicate : IValidationPredicate
    {
        private readonly Regex mRegex;
        private readonly string mPattern;

        public DoesMatchPredicate(Type parameterType, string pattern, RegexOptions? options = null, TimeSpan? timeout = null)
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

        public bool Validate(object value)
        {
            if (value == null)
                return false;
            return mRegex.IsMatch(value?.ToString());
        }

        public string RemoteScript(Type compilerType) => $"jsv_match(/{mPattern}/{Suffix(mRegex.Options)}, value)";

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