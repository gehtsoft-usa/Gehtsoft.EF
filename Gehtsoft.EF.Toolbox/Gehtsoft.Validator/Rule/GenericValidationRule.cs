namespace Gehtsoft.Validator
{
    public class GenericValidationRule<TE, TV> : ValidationRule
    {
        public GenericValidationRule() : base(typeof(TE), typeof(TV))
        {

        }
    }
}