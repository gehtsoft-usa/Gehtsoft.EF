using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.Validator;

namespace Gehtsoft.EF.Validator
{
    public class EfEntityValidator<T> : AbstractValidator<T>
    {
        public EfEntityValidator(SqlDbLanguageSpecifics specifics = null, IValidatorConnectionFactory connectionFactory = null, IEfValidatorMessageProvider messageProvider = null)
        {
            this.AddDbValidation(specifics, connectionFactory, messageProvider);
        }
    }
}