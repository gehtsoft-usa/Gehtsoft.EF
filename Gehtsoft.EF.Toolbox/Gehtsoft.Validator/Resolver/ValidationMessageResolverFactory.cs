using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.Validator
{
    public static class ValidationMessageResolverFactory
    {
        private static readonly Dictionary<Type, IValidationMessageResolver> mResolvers = new Dictionary<Type, IValidationMessageResolver>();
        private static readonly IValidationMessageResolver mNullResolver = new ValidationMessageNullResolver();

        public static IValidationMessageResolver GetResolver(Type entityType)
        {
            if (mResolvers.Count > 0 && mResolvers.TryGetValue(entityType, out IValidationMessageResolver resolver))
                return resolver;
            return mNullResolver;
        }

        public static void SetResolverFor(Type entityType, IValidationMessageResolver resolver) => mResolvers[entityType] = resolver;

        public static void SetResolverFor<T>(IValidationMessageResolver resolver) => SetResolverFor(typeof(T), resolver);
    }
}
