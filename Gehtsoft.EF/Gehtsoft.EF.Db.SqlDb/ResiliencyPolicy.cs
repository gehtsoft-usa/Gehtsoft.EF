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

    /// <summary>
    /// The interface to the resilience policy.
    ///
    /// If you need a good implementation of resiliency policy,
    /// consider [eurl=https://www.nuget.org/packages/Polly/]Polly[/eurl]
    /// package.
    /// </summary>
    public interface IResiliencyPolicy
    {
        /// <summary>
        /// Execute action.
        /// </summary>
        /// <param name="action"></param>
        void Execute(Action action);

        /// <summary>
        /// Execute action asyncronously.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="action"></param>
        /// <returns></returns>
        TResult Execute<TResult>(Func<TResult> action);

        /// <summary>
        /// Execute action asyncronously with cancellation token.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken token);

        /// <summary>
        /// Execution action that returns a value asynchronously with cancellation token.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="action"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken token);
    }

    /// <summary>
    /// The policy of protection against SQL injection.
    /// </summary>
    public class SqlInjectionProtectionPolicy
    {
        /// <summary>
        /// The flag indicating whether execution of the queries containing
        /// scalars shall be prevented.
        /// </summary>
        public bool ProtectFromScalarsInQueries { get; set; } = true;

        private static SqlInjectionProtectionPolicy mInstance;

        /// <summary>
        /// Gets an instance of a signleton policy object.
        /// </summary>
        public static SqlInjectionProtectionPolicy Instance => mInstance ?? (mInstance = new SqlInjectionProtectionPolicy());
    }

    /// <summary>
    /// The global resiliency policy.
    /// </summary>
    public sealed class ResiliencyPolicyDictionary
    {
        private static ResiliencyPolicyDictionary mInstance = null;

        /// <summary>
        /// Gets an instance of a signleton resiliency policy dictionary.
        /// </summary>
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

        /// <summary>
        /// Sets resiliency policy for all further connections.
        /// </summary>
        /// <param name="policy"></param>
        /// <param name="forceOverride">If flag is `false` the exception will be thrown in case the policy is already set.</param>
        public void SetGlobalPolicy(IResiliencyPolicy policy, bool forceOverride = false)
        {
            if (mGlobalResiliencyPolicy != null && !forceOverride)
                throw new InvalidOperationException("Global resiliency policy is already set");
            mGlobalResiliencyPolicy = policy;
        }

        /// <summary>
        /// Sets the resiliency policy for all further connections with the specified connection string.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="policy"></param>
        /// <param name="forceOverride">If flag is `false` the exception will be thrown in case the policy is already set.</param>
        public void SetPolicy(string connectionString, IResiliencyPolicy policy, bool forceOverride = false)
        {
            if (mConnectionResiliencyPolicies == null)
                mConnectionResiliencyPolicies = new ConcurrentDictionary<string, IResiliencyPolicy>();
            if (mConnectionResiliencyPolicies.ContainsKey(connectionString) && !forceOverride)
                throw new InvalidOperationException("Resiliency policy is already set");
            mConnectionResiliencyPolicies.AddOrUpdate(connectionString, policy, (key, value) => policy);
        }

        /// <summary>
        /// Gets policy to the specified connection string.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
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