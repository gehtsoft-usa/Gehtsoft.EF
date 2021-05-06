using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Mapper;
using Gehtsoft.EF.Mapper.Validator;
using Gehtsoft.EF.Validator;
using Gehtsoft.Validator;
using Gehtsoft.Validator.JSConvertor;
using NUnit.Framework;

namespace Gehtsoft.EF.Toolbox.Test
{
    [TestFixture]
    public class TestJsConvertor
    {
        [Entity]
        public class Entity
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty(Size = 32)]
            public string Name { get; set; }

            [EntityProperty(Size = 4, Precision = 1)]
            public double Age { get; set; }
        }

        public class SubModel
        {
            public string Email { get; set; }
            public bool AllowEmptyName { get; set; }
            public bool AllowAllowEmptyName { get; set; }
        }

        [MapEntity(typeof(Entity))]
        public class Model
        {
            [MapProperty]
            public int? ID { get; set; }

            [MapProperty]
            public string Name { get; set; }

            [MapProperty]
            public double Age { get; set; }

            public int[] Arr { get; set; }

            public DateTime? Dt { get; set; }

            public SubModel SubModel { get; set; }

            public int[] Array1 { get; set; }

            public SubModel[] Array2 { get; set; }
        }

        public class SubModelValidator : AbstractValidator<SubModel>
        {
            public SubModelValidator()
            {
                RuleFor(e => e.Email).EmailAddress().UnlessValue(v => string.IsNullOrEmpty(v)).WithMessage("Must be a correct email");
                RuleFor(e => e.Email).NotSQLInjection().WithMessage("Incorrect symbols 1");
                RuleFor(e => e.Email).NotHTML().WithMessage("Incorrect symbols 2");
                RuleFor(e => e.AllowEmptyName).Must(v => !v).UnlessEntity(e => e.AllowAllowEmptyName).WithMessage("Allowing an empty name isn't allowed");
            }
        }

        public class ModelValidator : EfModelValidator<Model>
        {
            public ModelValidator() : base()
            {
                ValidateModel(new DefaultEfValidatorMessageProvider());
                RuleForEntity().Must(e => e.SubModel != null).WithMessage("SubModel is substantial part of the submission, do not miss it!").ServerOnly();
                RuleFor(e => e.ID).Must(v => v.HasValue);
                RuleFor(e => e.Name).NotNullOrWhitespace().UnlessEntity(e => e.SubModel != null && e.SubModel.AllowAllowEmptyName).WithMessage("Empty name must be allowed");
                RuleFor(e => e.Name).Must(value => value.Substring(1).All(c => c != value[0]));
                RuleFor(e => e.Age).Must(v => v > 16).WithMessage("Age must be more than 16");
                RuleFor(e => e.SubModel).ValidateUsing<SubModelValidator>();
                RuleFor(e => e.Arr).Must(v => v.All(c => c >= v[0]));
                RuleFor(e => e.Dt).Must(v => IsXXCentury(v.Value.Year) && ContantProperty).WhenValue(v => v.HasValue);
                RuleFor(e => e.Age).Must(new ValueIsBetweenPredicate(typeof(int), 1, true, 100, false)).WithMessage("Age must be between 10 and 100");

                RuleFor(e => e.Age)
                    .WhenEntity(e => string.IsNullOrEmpty(e.Name))
                    .Must(v => v > 16).WithMessage("message1")
                    .Otherwise().EntityMust(e => e.ID == 0).WithMessage("Id must be null if name is null or empty")
                    .Also().EntityMust(e => e.Arr == null).WithMessage("Array must be if name is null or empty");

                RuleForAll(e => e.Array1)
                    .Must(v => v > 10)
                    .WithMessage("Must be above 10");

                RuleForAll(e => e.Array2).ValidateUsing<SubModelValidator>();
            }

            public static bool IsXXCentury(int year) => year >= 1900 && year < 2000;

            public bool ContantProperty => true;
        }

        [Explicit]
        [Test]
        public void TestJsValidator()
        {
            ValidationExpressionCompiler.AddCustomCall((expression, compiler) =>
            {
                if (expression.Object == null && expression.Method.Name == nameof(ModelValidator.IsXXCentury))
                {
                    return $"function (e) {{ e >= 1900 && e < 2000 }} ({ compiler(expression.Arguments[0]) })";
                }
                return null;
            });

            ValidationExpressionCompiler.AddCustomMemberAccess((expression, compiler) =>
            {
                if (expression.Expression.Type == typeof(ModelValidator) && expression.Member.Name == nameof(ModelValidator.ContantProperty))
                    return "true";
                return null;
            });

            ModelValidator validator = new ModelValidator();
            JsValidatorRule[] jsRules = validator.GetJsRules();

            Trace.WriteLine(jsRules);
        }
    }
}