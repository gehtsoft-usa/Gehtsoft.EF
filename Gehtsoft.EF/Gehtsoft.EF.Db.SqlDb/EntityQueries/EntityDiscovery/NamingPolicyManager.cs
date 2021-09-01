using System.Collections.Generic;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The manager of the naming policies.
    /// </summary>
    public class NamingPolicyManager
    {
        /// <summary>
        /// Default naming policy.
        /// 
        /// The default naming policy is <see cref="EntityNamingPolicy.BackwardCompatibility"/>
        /// </summary>
        public EntityNamingPolicy Default { get; set; } = EntityNamingPolicy.BackwardCompatibility;
        private readonly Dictionary<string, EntityNamingPolicy> mNamingPolicies = new Dictionary<string, EntityNamingPolicy>();
        private const string DEFAULTSCOPE = "gs$$defaultscope";

        /// <summary>
        /// Gets or sets naming policy for the specified scope.
        /// </summary>
        /// <param name="scope"></param>
        /// <returns></returns>
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
