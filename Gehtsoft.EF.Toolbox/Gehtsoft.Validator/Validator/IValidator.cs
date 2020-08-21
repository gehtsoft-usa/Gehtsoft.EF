using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.Validator
{
    public interface IBaseValidator : IEnumerable<IValidationRule>
    {
        Type ValidateType { get; }
        ValidationResult Validate(object entity);
    }


    public interface IValidator<T> : IBaseValidator
    {
        ValidationResult Validate(T entity);
    }

    public interface IAsyncBaseValidator 
    {
        Task<ValidationResult> ValidateAsync(object entity, CancellationToken? token = null);
    }

    public interface IAsyncValidator<T> : IAsyncBaseValidator
    {
        Task<ValidationResult> ValidateAsync(T entity);
    }
}