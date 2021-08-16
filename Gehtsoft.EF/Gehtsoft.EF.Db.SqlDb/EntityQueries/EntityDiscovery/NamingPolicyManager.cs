using System.Collections.Generic;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class NamingPolicyManager
    {
        public EntityNamingPolicy Default { get; set; } = EntityNamingPolicy.BackwardCompatibility;
        private readonly Dictionary<string, EntityNamingPolicy> mNamingPolicies = new Dictionary<string, EntityNamingPolicy>();
        private const string DEFAULTSCOPE = "gs$$defaultscope";

        public EntityNamingPolicy this[string scope]
        {
            get
            {
                if (scope == null)
                    return Default;

                if (!mNamingPolicies.TryGetValue(scope, out EntityNamingPolicy policy))
                    policy = Default;

                return policy;
            }
            set => mNamingPolicies[scope ?? DEFAULTSCOPE] = value;
        }
    }
}
