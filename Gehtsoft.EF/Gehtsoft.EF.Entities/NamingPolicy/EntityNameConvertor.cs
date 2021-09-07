using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// The name converter used for entity policies
    /// </summary>
    public static class EntityNameConvertor
    {
        private static bool IsVowel(char c) => c == 'a' || c == 'o' || c == 'i' || c == 'e' || c == 'y' || c == 'u';

        /// <summary>
        /// Converts the table name according the policy specified.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="policy"></param>
        /// <returns></returns>
        public static string ConvertTableName(string name, EntityNamingPolicy? policy)
        {
            if (name.Length > 2 && policy != EntityNamingPolicy.AsIs)
            {
                char preLast = char.ToLower(name[name.Length - 2]);
                char last = char.ToLower(name[name.Length - 1]);
                if (last == 'y' && !IsVowel(preLast))
                    name = name.Substring(0, name.Length - 1) + "ies";
                else if (last == 's' || last == 'x' || last == 'z' || (last == 'h' && (preLast == 'c' || preLast == 's')))
                {
                    if (name.Equals("fez", StringComparison.OrdinalIgnoreCase))
                        name = "fezz";
                    else if (name.Equals("gas", StringComparison.OrdinalIgnoreCase))
                        name = "gass";
                    name += "es";
                }
                else if (last == 'o' && !IsVowel(preLast))
                {
                    if (name.EndsWith("hero", StringComparison.OrdinalIgnoreCase) || name.EndsWith("potato", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith("tomato", StringComparison.OrdinalIgnoreCase) || name.EndsWith("volcano", StringComparison.OrdinalIgnoreCase))
                        name += "es";
                    else
                        name += "s";
                }
                else if ((last == 'f' || (last == 'e' && preLast == 'f')) &&
                    !name.EndsWith("roof", StringComparison.OrdinalIgnoreCase) &&
                    !name.EndsWith("belief", StringComparison.OrdinalIgnoreCase) &&
                    !name.EndsWith("chief", StringComparison.OrdinalIgnoreCase) &&
                    !name.EndsWith("chef", StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(0, name.Length - (last == 'f' ? 1 : 2)) + "ves";
                else
                    name += "s";
            }
            return ConvertName(name, policy);
        }

        /// <summary>
        /// Converts a column name according the policy specified.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="policy"></param>
        /// <returns></returns>
        public static string ConvertName(string name, EntityNamingPolicy? policy)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            switch (policy ?? EntityNamingPolicy.AsIs)
            {
                case EntityNamingPolicy.AsIs:
                    return name;
                case EntityNamingPolicy.BackwardCompatibility:
                case EntityNamingPolicy.LowerCase:
                    return name.ToLower();
                case EntityNamingPolicy.UpperCase:
                    return name.ToUpper();
                case EntityNamingPolicy.LowerFirstCharacter:
                    return char.ToLower(name[0]) + name.Substring(1);
                case EntityNamingPolicy.UpperFirstCharacter:
                    return char.ToUpper(name[0]) + name.Substring(1);
                case EntityNamingPolicy.LowerCaseWithUnderscores:
                    {
                        StringBuilder b = new StringBuilder();
                        bool priorIsUpper = false;
                        foreach (char c in name)
                        {
                            bool isUpper = char.IsUpper(c);
                            bool invertedCase = char.IsUpper(c) && !priorIsUpper && b.Length > 0;
                            priorIsUpper = isUpper;
                            if (invertedCase)
                                b.Append('_');
                            b.Append(char.ToLower(c));
                        }
                        return b.ToString();
                    }
                case EntityNamingPolicy.UpperCaseWithUnderscopes:
                    {
                        StringBuilder b = new StringBuilder();
                        bool priorIsUpper = false;
                        foreach (char c in name)
                        {
                            bool isUpper = char.IsUpper(c);
                            bool invertedCase = char.IsUpper(c) && !priorIsUpper && b.Length > 0;
                            priorIsUpper = isUpper;
                            if (invertedCase)
                                b.Append('_');
                            b.Append(char.ToUpper(c));
                        }
                        return b.ToString();
                    }
                default:
                    throw new ArgumentException("Unsupported naming policy", nameof(policy));
            }
        }
    }
}
