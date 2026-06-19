using System;
using System.Collections.Generic;
using Gehtsoft.ExpressionToJs;
using Gehtsoft.Validator.JSConvertor;
using Jint;

namespace Gehtsoft.EF.Toolbox.Test
{
    /// <summary>
    /// Executes the JS validation rules produced by <see cref="ConvertToJsExtension.GetJsRules"/>
    /// the way a browser-side validation engine does: the stub.js runtime from
    /// Gehtsoft.ExpressionToJs plus the reference()/value/index bindings the generated
    /// expressions rely upon.
    /// </summary>
    internal static class JsRuleExecutor
    {
        private const string Runtime = @"
var index = 0;
function reference(path) {
    if (path === undefined || path === null || path === '')
        return __model;
    var parts = path.split('.');
    var current = __model;
    for (var i = 0; i < parts.length; i++) {
        if (current === null || current === undefined)
            return current;
        var part = parts[i];
        if (part.length > 7 && part.substring(part.length - 7) === '[index]')
            current = element_at(current[part.substring(0, part.length - 7)]);
        else
            current = current[part];
    }
    return current;
}
function element_at(array) {
    return (array === null || array === undefined) ? undefined : jsv_index(array, index);
}";

        public static List<(string Path, string Message)> Validate(JsValidatorRule[] rules, object model)
        {
            Engine engine = new Engine();
            engine.Execute(ExpressionToJsStubAccessor.GetJsIncludesAsString());
            engine.SetValue("__model", model);
            engine.Execute(Runtime);

            var failures = new List<(string Path, string Message)>();

            foreach (JsValidatorRule rule in rules)
            {
                if (!rule.ArrayValidator)
                {
                    engine.Execute($"value = reference('{rule.JsTargetName}')");
                    if (RuleFails(engine, rule))
                        failures.Add((rule.JsTargetName, rule.ErrorMessage));
                }
                else
                {
                    string arrayPath = ArrayPath(rule.JsTargetName, out string elementPath);
                    int length = (int)engine.Evaluate($"jsv_length(reference('{arrayPath}'))").AsNumber();
                    for (int i = 0; i < length; i++)
                    {
                        engine.Execute($"index = {i}; value = reference('{elementPath}')");
                        if (RuleFails(engine, rule))
                            failures.Add((elementPath.Replace("[index]", $"[{i}]"), rule.ErrorMessage));
                    }
                }
            }
            return failures;
        }

        private static string ArrayPath(string targetName, out string elementPath)
        {
            int tokenAt = targetName.IndexOf("[index]", StringComparison.Ordinal);
            if (tokenAt < 0)
            {
                elementPath = targetName + "[index]";
                return targetName;
            }
            elementPath = targetName;
            return targetName.Substring(0, tokenAt);
        }

        private static bool RuleFails(Engine engine, JsValidatorRule rule)
        {
            if (rule.JsWhenExpression != null && !engine.Evaluate($"!!({rule.JsWhenExpression})").AsBoolean())
                return false;
            return !engine.Evaluate($"!!({rule.JsValidationExpression})").AsBoolean();
        }
    }
}
