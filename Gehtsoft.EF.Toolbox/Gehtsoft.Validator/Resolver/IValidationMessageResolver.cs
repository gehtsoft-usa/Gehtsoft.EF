using System;
using System.Reflection;

namespace Gehtsoft.Validator
{
    public interface IValidationMessageResolver
    {
        string Resolve(Type entity, ValidationTarget target, int code, string message);
    }
}