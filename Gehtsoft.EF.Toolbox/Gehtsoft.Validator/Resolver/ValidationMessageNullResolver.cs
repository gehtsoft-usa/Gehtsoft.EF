using System;
using System.Reflection;

namespace Gehtsoft.Validator
{
    public class ValidationMessageNullResolver : IValidationMessageResolver
    {
        public string Resolve(Type entityType, ValidationTarget target, int code, string message) => message;
    }
}