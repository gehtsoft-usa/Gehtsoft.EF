using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Entities
{
    public static class EntityNameConvertor
    {
        public static string ConvertTableName(string name, EntityNamingPolicy? policy)
        {
            if (name.Length > 2 && policy != EntityNamingPolicy.AsIs)
            {
                char preLast = char.ToLower(name[name.Length - 2]);
                char last = char.ToLower(name[name.Length - 1]);
                if (last == 'y')
                    name = name.Substring(0, name.Length - 1) + "ies";
                else if (last == 's' || last == 'x' || (last == 'h' && (preLast == 'c' || preLast == 's')))
                    name = name + "es";
                else if (last == 'o' && (preLast != 'a' && preLast != 'o' && preLast != 'i' && preLast != 'e' && preLast != 'y' && preLast != 'u'))
                {
                    if (name.EndsWith("hero", StringComparison.OrdinalIgnoreCase) || name.EndsWith("potato", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith("tomato", StringComparison.OrdinalIgnoreCase) || name.EndsWith("volcano", StringComparison.OrdinalIgnoreCase))
                        name = name + "es";
                    else
                        name = name + "s";
                }
                else
                    name = name + "s";
            }
            return ConvertName(name, policy);
        }

        public static string ConvertName(string name, EntityNamingPolicy? policy)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            switch (policy ?? EntityNamingPolicy.AsIs)
            {
                case EntityNamingPolicy.AsIs:
                    return name;
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
