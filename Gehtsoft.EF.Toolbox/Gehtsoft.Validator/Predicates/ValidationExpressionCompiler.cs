using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Gehtsoft.ExpressionToJs;

namespace Gehtsoft.Validator.JSConvertor
{
    public class ValidationExpressionCompiler : ExpressionCompiler
    {
        public ValidationExpressionCompiler(LambdaExpression lambdaExpression, int? entityParameterIndex = null, int? valueParameterIndex = null)
            : base(lambdaExpression)
        {
            if ((lambdaExpression.Parameters.Count < 1 && (entityParameterIndex != null || valueParameterIndex != null)) || lambdaExpression.Parameters.Count > 2)
                throw new ArgumentException("The expression must have only one or two parameters", nameof(lambdaExpression));

            // ExpressionPredicate always sets exactly one of these (and the rule is single-parameter),
            // so the binding targets that one root parameter regardless of its type -> the match is _ => true.
            // Nested LINQ-lambda parameters are not root parameters, so they keep their names automatically.
            if (entityParameterIndex != null)
            {
                // m.Password -> reference('Password'); m.Scores[0] -> jsv_index(reference('Scores'), 0)
                Parameters.MapReference(_ => true);
            }
            else if (valueParameterIndex != null)
            {
                // the field, rendered as the object 'value'. Since 0.3.2 the library decomposes array
                // indexing inside Map (like MapReference), so parameterAccess only ever sees a member
                // chain or the bare parameter and ParameterAccessPath no longer throws on ArrayIndex:
                //   v        -> value
                //   v.Length -> jsv_length(value)
                //   v[0]     -> jsv_index(value, 0)
                Parameters.Map(_ => true,
                               p => "value",
                               (e, p) => "value." + ParameterAccessPath(e));
            }

            // Bridge the process-global custom handlers to this instance's registries, evaluated at
            // emit time (so handlers registered before JavaScriptExpression is read still apply).
            Members.AddTranslator(new GlobalMemberHandlers());
            Methods.AddTranslator(new GlobalCallHandlers());
        }

        private static readonly object mCustomMutex = new object();
        private static readonly List<Func<MemberExpression, Func<Expression, string>, string>> mCustomMembers = new List<Func<MemberExpression, Func<Expression, string>, string>>();
        private static readonly List<Func<MethodCallExpression, Func<Expression, string>, string>> mCustomCalls = new List<Func<MethodCallExpression, Func<Expression, string>, string>>();

        public static void AddCustomMemberAccess(Func<MemberExpression, Func<Expression, string>, string> handler)
        {
            lock (mCustomMutex)
                mCustomMembers.Add(handler);
        }

        public static void AddCustomCall(Func<MethodCallExpression, Func<Expression, string>, string> handler)
        {
            lock (mCustomMutex)
                mCustomCalls.Add(handler);
        }

        // Adapters: a single translator each, iterating the global list at emit time (preserves the
        // old global + dynamic semantics; returns false so the built-ins still run when none matches).
        // User registrations run before built-ins, so custom handlers keep priority - matching the
        // old "custom first, then base" override order.
        private sealed class GlobalMemberHandlers : IMemberTranslator
        {
            public bool TryTranslate(MemberExpression member, IExpressionEmitContext context, out string js)
            {
                lock (mCustomMutex)
                {
                    foreach (var h in mCustomMembers)
                        if ((js = h?.Invoke(member, context.Emit)) != null)
                            return true;
                }
                js = null;
                return false;
            }
        }

        private sealed class GlobalCallHandlers : IMethodCallTranslator
        {
            public bool TryTranslate(MethodCallExpression call, IExpressionEmitContext context, out string js)
            {
                lock (mCustomMutex)
                {
                    foreach (var h in mCustomCalls)
                        if ((js = h?.Invoke(call, context.Emit)) != null)
                            return true;
                }
                js = null;
                return false;
            }
        }
    }
}
