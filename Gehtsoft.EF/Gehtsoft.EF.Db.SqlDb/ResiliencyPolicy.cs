using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb
{
    internal static class StringExtension
    {
        public static bool ContainsScalar(this string s, bool wholeQuery = false)
        {
            if (s == null)
                return false;

            int l = s.Length;

            if (l == 0)
                return false;

            for (int i = 0; i < l; i++)
            {
                char c = s[i];
                if (c == '\'' || c == '\"')
                    return true;
                if (c == ';' && !wholeQuery)
                    return true;
            }
            return false;
        }
    }

    public interface IResiliencyPolicy
    {
        void Execute(Action action);

        TResult Execute<TResult>(Func<TResult> action);

        Task ExecuteAsync(Func<Task> action);

        Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken token);

        Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> action);

        Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken token);
    }

    public class SqlInjectionProtectionPolicy
    {
        public bool ProtectFromScalarsInQueries { get; set; } = true;

        private static SqlInjectionProtectionPolicy mInstance;

        public static SqlInjectionProtectionPolicy Instance => mInstance ?? (mInstance = new SqlInjectionProtectionPolicy());
    }

    public sealed class ResiliencyPolicyDictionary
    {
        private static ResiliencyPolicyDictionary mInstance = null;

        public static ResiliencyPolicyDictionary Instance
        {
            get
            {
                return mInstance ?? (mInstance = new ResiliencyPolicyDictionary());
            }
        }

        private IResiliencyPolicy mGlobalResiliencyPolicy = null;

        private ConcurrentDictionary<string, IResiliencyPolicy> mConnectionResiliencyPolicies = null;

        private ResiliencyPolicyDictionary()
        {
        }

        public void SetGlobalPolicy(IResiliencyPolicy policy, bool forceOverride = false)
        {
            if (mGlobalResiliencyPolicy != null && !forceOverride)
                throw new InvalidOperationException("Global resiliency policy is already set");
            mGlobalResiliencyPolicy = policy;
        }

        public void SetPolicy(string connectionString, IResiliencyPolicy policy, bool forceOverride = false)
        {
            if (mConnectionResiliencyPolicies == null)
                mConnectionResiliencyPolicies = new ConcurrentDictionary<string, IResiliencyPolicy>();
            if (mConnectionResiliencyPolicies.ContainsKey(connectionString) && !forceOverride)
                throw new InvalidOperationException("Resiliency policy is already set");
            mConnectionResiliencyPolicies.AddOrUpdate(connectionString, policy, (key, value) => policy);
        }

        public IResiliencyPolicy GetPolicy(string connectionString)
        {
            if (mConnectionResiliencyPolicies != null)
            {
                if (mConnectionResiliencyPolicies.TryGetValue(connectionString, out IResiliencyPolicy policy))
                    return policy;
            }
            return mGlobalResiliencyPolicy;
        }
    }
}