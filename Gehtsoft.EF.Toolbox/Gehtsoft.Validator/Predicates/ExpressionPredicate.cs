using System;
using System.Linq.Expressions;
using Gehtsoft.ExpressionToJs;
using Gehtsoft.Validator.JSConvertor;

namespace Gehtsoft.Validator
{
    public class ExpressionPredicate<T> : FunctionPredicate<T>
    {
        private Expression<Func<T, bool>> mPredicateSource;
        private string mJavaScript = null;
        private readonly bool mParameterIsEntity;

        public ExpressionPredicate(Expression<Func<T, bool>> predicateSource, bool parameterIsEntity = false) : base(predicateSource.Compile())
        {
            mPredicateSource = predicateSource;
            mParameterIsEntity = parameterIsEntity;
        }

        public override string RemoteScript(Type expressionCompilerType)
        {
            if (expressionCompilerType == null)
                throw new ArgumentNullException(nameof(expressionCompilerType));

            if (mPredicateSource == null)
                return mJavaScript;

            ValidationExpressionCompiler compiler;
            try
            {
                compiler = Activator.CreateInstance(expressionCompilerType, mPredicateSource, mParameterIsEntity ? (int?)0 : null, mParameterIsEntity ? null : (int?)0) as ValidationExpressionCompiler;
            }
            catch (Exception)
            {
                compiler = null;
            }

            mPredicateSource = null;

            if (compiler == null)
                throw new ArgumentException($"The compiler type must be derived from {nameof(ValidationExpressionCompiler)} and has the same signature of the constructor", nameof(expressionCompilerType));

            try
            {
                mJavaScript = compiler.JavaScriptExpression;
            }
            catch (Exception)
            {
                mJavaScript = null;
                throw;
            }

            return mJavaScript;
        }
    }
}