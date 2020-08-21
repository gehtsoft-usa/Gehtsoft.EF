using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
                if (rule.Validator != null && !rule.IgnoreOnClient)
                {
                    JsValidatorRule jsRule = new JsValidatorRule()
                    {
                        JsTargetName = hasPrefix ? $"{prefix}.{rule.Target.TargetName}" : rule.Target.TargetName,
                        ArrayValidator = !rule.Target.IsSingleValue || isArrayValidator,
                        JsValidationExpression = rule.Validator.RemoteScript(compilerType),
                        ErrorMessage = rule.Message,
                    };


                    if (jsRule.JsValidationExpression == null) //isn't supported on the client
                        continue;

                    if (!string.IsNullOrEmpty(prefix))
                        jsRule.JsValidationExpression = jsRule.JsValidationExpression.Replace("reference('", $"reference('{prefix}.");


                    bool hasWhen = true;
                    if (rule.WhenEntity != null)
                        jsRule.JsWhenExpression = rule.WhenEntity.RemoteScript(compilerType);
                    else if (rule.WhenValue != null)
                        jsRule.JsWhenExpression = rule.WhenValue.RemoteScript(compilerType);
                    else if (rule.UnlessEntity != null)
                        jsRule.JsWhenExpression = $"!({rule.UnlessEntity.RemoteScript(compilerType)})";
                    else if (rule.UnlessValue != null)
                        jsRule.JsWhenExpression = $"!({rule.UnlessValue.RemoteScript(compilerType)})";
                    else
                        hasWhen = false;

                    if (hasWhen && jsRule.JsValidationExpression == null)
                        throw new ArgumentNullException($"Rule {jsRule.JsTargetName} has a condition predicate which can't be compiled to the JS.");

                    if (hasWhen && !string.IsNullOrEmpty(prefix))
                            jsRule.JsWhenExpression = jsRule.JsWhenExpression.Replace("reference('", $"reference('{prefix}.");

                    list.Add(jsRule);
                }
                else if (rule.HasAnotherValidator && !rule.IgnoreOnClient)
                {
                    bool isArray = !rule.Target.IsSingleValue;
                    string pprefix = hasPrefix ? $"{prefix}.{rule.Target.TargetName}" : rule.Target.TargetName;
                    if (isArray)
                        pprefix += "[index]";
                    ScanJsRules(rule.AnotherValidator, list, compilerType, pprefix, isArray || isArrayValidator);
                }
            }
        }
    }
}
