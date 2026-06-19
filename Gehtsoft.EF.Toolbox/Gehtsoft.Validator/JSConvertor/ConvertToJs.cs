using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.ExpressionToJs;

namespace Gehtsoft.Validator.JSConvertor
{
    public class JsValidatorRule
    {
        public string JsTargetName { get; set; }
        public bool ArrayValidator { get; set; }
        public string JsValidationExpression { get; set; }
        public string JsWhenExpression { get; set; }
        public string ErrorMessage { get; set; }
    }

    public static class ConvertToJsExtension
    {
        public static JsValidatorRule[] GetJsRules(this IBaseValidator validator, Type compilerType = null)
        {
            List<JsValidatorRule> rules = new List<JsValidatorRule>();
            if (compilerType == null)
                compilerType = typeof(ValidationExpressionCompiler);
            ScanJsRules(validator, rules, compilerType, null);
            return rules.ToArray();
        }

        private static void ScanJsRules(IBaseValidator validator, List<JsValidatorRule> list, Type compilerType, string prefix, bool isArrayValidator = false)
        {
            bool hasPrefix = !string.IsNullOrEmpty(prefix);
            foreach (IValidationRule rule in validator)
            {
                if (rule.Side == RuleExecutionSide.Server)
                    continue;

                if (rule.Validator != null)
                {
                    JsValidatorRule jsRule = new JsValidatorRule()
                    {
                        JsTargetName = hasPrefix ? $"{prefix}.{rule.Target.TargetName}" : rule.Target.TargetName,
                        ArrayValidator = !rule.Target.IsSingleValue || isArrayValidator,
                        JsValidationExpression = rule.Validator.RemoteScript(compilerType),
                        ErrorMessage = rule.Message,
                    };

                    if (jsRule.JsValidationExpression == null)
                        throw new InvalidOperationException($"The validation predicate of the rule {jsRule.JsTargetName} cannot be translated to JS. Mark the rule with SetSide(RuleExecutionSide.Server) if the rule is intended to be validated on the server only.");

                    if (hasPrefix)
                        jsRule.JsValidationExpression = jsRule.JsValidationExpression.Replace("reference('", $"reference('{prefix}.");

                    if (rule.WhenEntity != null)
                        jsRule.JsWhenExpression = ConditionScript(rule.WhenEntity, compilerType, jsRule.JsTargetName, negate: false);
                    else if (rule.WhenValue != null)
                        jsRule.JsWhenExpression = ConditionScript(rule.WhenValue, compilerType, jsRule.JsTargetName, negate: false);
                    else if (rule.UnlessEntity != null)
                        jsRule.JsWhenExpression = ConditionScript(rule.UnlessEntity, compilerType, jsRule.JsTargetName, negate: true);
                    else if (rule.UnlessValue != null)
                        jsRule.JsWhenExpression = ConditionScript(rule.UnlessValue, compilerType, jsRule.JsTargetName, negate: true);

                    if (jsRule.JsWhenExpression != null && hasPrefix)
                        jsRule.JsWhenExpression = jsRule.JsWhenExpression.Replace("reference('", $"reference('{prefix}.");

                    list.Add(jsRule);
                }
                else if (rule.HasAnotherValidator)
                {
                    bool isArray = !rule.Target.IsSingleValue;
                    string pprefix = hasPrefix ? $"{prefix}.{rule.Target.TargetName}" : rule.Target.TargetName;
                    if (isArray)
                        pprefix += "[index]";
                    ScanJsRules(rule.AnotherValidator, list, compilerType, pprefix, isArray || isArrayValidator);
                }
            }
        }

        private static string ConditionScript(IValidationPredicate predicate, Type compilerType, string targetName, bool negate)
        {
            string script = predicate.RemoteScript(compilerType);
            if (script == null)
                throw new InvalidOperationException($"The condition predicate of the rule {targetName} cannot be translated to JS. Mark the rule with SetSide(RuleExecutionSide.Server) if the rule is intended to be validated on the server only.");
            return negate ? $"!({script})" : script;
        }
    }
}
