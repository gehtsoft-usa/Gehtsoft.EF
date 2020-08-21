namespace Gehtsoft.Validator
{
    public class ValidationResult
    {
        public bool IsValid => (mFailures?.Count ?? 0) == 0;

        private ValidationFailureCollection mFailures = null;

        public ValidationFailureCollection Failures => mFailures ?? (mFailures = new ValidationFailureCollection());
    }
}
