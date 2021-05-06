using System;

namespace Gehtsoft.Validator
{
    public class CreditCardNumberPredicate : IValidationPredicate
    {
        private readonly Type mParameterType;
        public Type ParameterType => mParameterType;

        public CreditCardNumberPredicate(Type type)
        {
            mParameterType = type;
        }

        public bool Validate(object _value)
        {
            if (_value == null)
                return false;

            if (!(_value is string value))
                return false;

            value = value.Replace("-", "").Replace(" ", "");

            int checksum = 0;
            bool evenDigit = false;
            char[] digits = value.ToCharArray();

            for (int j = digits.Length - 1; j >= 0; j--)
            {
                char digit = digits[j];
                if (!char.IsDigit(digit))
                    return false;

                int digitValue = (digit - '0') * (evenDigit ? 2 : 1);
                evenDigit = !evenDigit;

                while (digitValue > 0)
                {
                    checksum += digitValue % 10;
                    digitValue /= 10;
                }
            }
            return (checksum % 10) == 0;
        }

        public string RemoteScript(Type compilerType) => "jsv_ccn_valid(value)";
    }
}