# SKILL: Gehtsoft.Validator / Gehtsoft.EF.Validator / Gehtsoft.EF.Mapper.Validator

## Overview

A fluent, rule-based object validation framework for .NET Standard 2.0 with deep integration with Gehtsoft.EF entities and the Gehtsoft.Mapper library.

- **Gehtsoft.Validator** - generic validation framework (namespace: `Gehtsoft.Validator`).
- **Gehtsoft.EF.Validator** - automatic EF entity validation from entity metadata (namespace: `Gehtsoft.EF.Validator`).
- **Gehtsoft.EF.Mapper.Validator** - validates mapper model classes against DB constraints of their mapped EF entities (namespace: `Gehtsoft.EF.Mapper.Validator`).

## NuGet Packages

| Package | Dependencies |
|---|---|
| `Gehtsoft.Validator` | `Gehtsoft.ExpressionToJs` |
| `Gehtsoft.EF.Validator` | `Gehtsoft.Validator`, `Gehtsoft.EF.Db.SqlDb`, `Gehtsoft.EF.Entities` |
| `Gehtsoft.EF.Mapper.Validator` | `Gehtsoft.EF.Mapper`, `Gehtsoft.EF.Validator`, `Gehtsoft.Validator` |

---

## Gehtsoft.Validator

### Core Types

#### AbstractValidator\<T\>

The primary base class for defining validators. Derive from it and define rules in the constructor.

```csharp
public class PersonValidator : AbstractValidator<Person>
{
    public PersonValidator()
    {
        // Property rules
        RuleFor(p => p.Name)
            .NotNullOrWhitespace()
            .ShorterThan(101)
            .WithMessage("Name is required and must be 100 chars or less");

        RuleFor(p => p.Age)
            .Must(age => age >= 0 && age <= 150)
            .WithCode(1001);

        // Entity-level rule
        RuleForEntity("CrossFieldCheck")
            .Must(p => p.StartDate <= p.EndDate)
            .WithMessage("Start date must be before end date");

        // Array element validation
        RuleForAll(p => p.Tags)
            .NotNullOrWhitespace();

        // Delegate to another validator
        RuleFor(p => p.Address)
            .ValidateUsing<AddressValidator>();

        // Conditional validator
        When(p => p.IsActive);          // only validate if active
        // or: Unless(p => p.IsDeleted);
    }
}
```

#### Validation Execution

```csharp
var validator = new PersonValidator();
ValidationResult result = validator.Validate(person);

if (!result.IsValid)
{
    foreach (ValidationFailure failure in result.Failures)
    {
        Console.WriteLine($"Property: {failure.Name}");
        Console.WriteLine($"Path: {failure.Path}");       // e.g. "Address.City" for nested
        Console.WriteLine($"Code: {failure.Code}");
        Console.WriteLine($"Message: {failure.Message}");
    }
}
```

#### ValidationResult

| Property | Type | Description |
|---|---|---|
| `IsValid` | bool | True if no failures |
| `Failures` | ValidationFailureCollection | List of all validation failures |

#### ValidationFailure

| Property | Type | Description |
|---|---|---|
| `Name` | string | Name of the failed value/property |
| `Path` | string | Full path (e.g. `Address.City` when using `ValidateUsing`) |
| `Code` | int | Error code (0 if not set) |
| `Message` | string | Error message (null if not set) |

### Rule Builder Methods

#### AbstractValidator\<T\> Methods

| Method | Returns | Description |
|---|---|---|
| `RuleFor<TV>(p => p.Prop)` | `GenericValidationRuleBuilder<T,TV>` | Rule for a property |
| `RuleFor<TV>(p => p.Prop, "name")` | `GenericValidationRuleBuilder<T,TV>` | Rule with custom name |
| `RuleForEntity("name")` | `GenericValidationRuleBuilder<T,T>` | Rule for the whole entity |
| `RuleForAll<TV>(p => p.Collection)` | `GenericValidationRuleBuilder<T,TV>` | Rule for each element |
| `When(predicate)` | void | Only validate when predicate is true |
| `Unless(predicate)` | void | Only validate when predicate is false |

#### GenericValidationRuleBuilder\<TE, TV\> (fluent)

**Predicates** - what must be true:

| Method | Description |
|---|---|
| `.Must(v => expr)` | Custom LINQ expression predicate |
| `.NotNull()` | Value must not be null |
| `.Null()` | Value must be null |
| `.NotNullOrEmpty()` | Not null/empty (strings, arrays, collections, enumerables) |
| `.NotNullOrWhitespace()` | Not null/empty/whitespace string |
| `.ShorterThan(n)` | Length < n (first invalid length, not max valid length) |
| `.Between(min, max)` | Value in range [min, max] (inclusive) |
| `.Between(min, minIncl, max, maxIncl)` | Value in range with explicit inclusivity |
| `.DoesMatch(pattern)` | Regex match (partial unless starts with `^`) |
| `.DoesNotMatch(pattern)` | Regex non-match |
| `.EnumIsCorrect()` | Enum value is defined |
| `.EnumIsCorrect<TEnum>()` | Enum value is defined (explicit type) |
| `.EmailAddress()` | Valid email format |
| `.PhoneNumber()` | Valid phone number |
| `.CreditCardNumber()` | Valid credit card (Luhn) |
| `.NotSQLInjection()` | No SQL injection patterns |
| `.NotHTML()` | No HTML injection patterns |

**Conditions** - when to apply the rule:

| Method | Description |
|---|---|
| `.WhenValue(v => expr)` | Apply rule only when value matches |
| `.UnlessValue(v => expr)` | Apply rule only when value doesn't match |
| `.WhenEntity(e => expr)` | Apply rule only when entity matches |
| `.UnlessEntity(e => expr)` | Apply rule only when entity doesn't match |
| `.WhenNotNull()` | Skip rule if value is null |

**Metadata**:

| Method | Description |
|---|---|
| `.WithCode(int)` | Set error code on failure |
| `.WithMessage(string)` | Set error message (localized via resolver if set) |
| `.ServerOnly()` | Exclude from JavaScript compilation |

**Delegation**:

| Method | Description |
|---|---|
| `.ValidateUsing(typeof(V))` | Validate using another validator type |
| `.ValidateUsing<V>()` | Validate using another validator (generic) |
| `.ValidateUsing(instance)` | Validate using a validator instance |
| `.ValidateUsing(type, args)` | Validator with constructor args |

**Chaining**:

| Method | Description |
|---|---|
| `.Otherwise()` | New rule, same target, opposite condition |
| `.Also()` | New rule, same target, same condition |

### Attribute-Based Validation

Validation attributes can be placed on properties. They are recognized when the validator is constructed using the base class infrastructure.

| Attribute | Description |
|---|---|
| `[MustBeNotNull]` | Value must not be null |
| `[MustBeNotNullOrWhitespace]` | Value must not be null/empty/whitespace |
| `[MustBeShorterThan(length)]` | String length < length |
| `[MustBeInRange(min, max)]` | Value between min and max |
| `[MustMatch(pattern)]` | Regex match |
| `[MustBeNotEmpty]` | Collection/string must not be empty |

All attributes derive from `ValidatorAttributeBase` and support `WidthCode` and `WithMessage` properties.

### Message Resolution

```csharp
// Set a global message resolver
ValidationMessageResolverFactory.SetResolver(new MyResolver());

// Resolver interface
public interface IValidationMessageResolver
{
    string Resolve(string message);
}
```

When `.WithMessage("key")` is used and a resolver is registered, the message is passed through the resolver for localization.

### JavaScript Compilation

Validation rules can be compiled to JavaScript for client-side validation.

```csharp
using Gehtsoft.Validator.JSConvertor;

var jsRules = validator.ConvertToJs();
```

**Supported predicates** for JS compilation:
- Null, NotNull, NotNullOrEmpty
- ShorterThan, Between
- DoesMatch, DoesNotMatch
- LINQ expressions using basic operators (+, -, *, /, %, ==, !=, >, <, >=, <=, &&, ||, !)
- String methods: Length, Upper, Lower, Trim, StartsWith, IndexOf, Substring
- DateTime properties: Year, Month, Day, Hour, Minute, Second
- Nullable: HasValue, Value
- LINQ: Any, All, Count, First, Last with predicates

**Not supported** (server-only): Function predicates, enum validation, uniqueness, existence, unsupported expression nodes.

Mark rules explicitly with `.ServerOnly()` to prevent JS compilation attempts.

Requires `Gehtsoft.ExpressionToJs` package.

---

## Gehtsoft.EF.Validator

### EfEntityValidator\<T\>

Automatically validates EF entities based on entity metadata. No manual rule definitions needed for standard DB constraints.

```csharp
var validator = new EfEntityValidator<MyEntity>(
    specifics: connection.GetLanguageSpecifics(),   // optional
    connectionFactory: new ValidatorConnectionFactory(connString, driver)  // optional
);

ValidationResult result = validator.Validate(entity);
```

**Constructor parameters**:

| Parameter | Type | Required | Purpose |
|---|---|---|---|
| `specifics` | `SqlDbLanguageSpecifics` | No | Enables date/timestamp range validation |
| `connectionFactory` | `IValidatorConnectionFactory` | No | Enables uniqueness and FK existence checks |
| `messageProvider` | `IEfValidatorMessageProvider` | No | Custom error messages |

**Automatically validates**:
- Non-nullable properties are not null
- String length does not exceed column size
- Numeric values fit within column precision/scale
- Date/DateTime values within DB-specific range (requires `specifics`)
- Unique column values are actually unique (requires `connectionFactory`)
- Foreign key references exist in DB (requires `connectionFactory`)

Since `EfEntityValidator<T>` extends `AbstractValidator<T>`, you can add custom rules on top:

```csharp
public class MyEntityValidator : EfEntityValidator<MyEntity>
{
    public MyEntityValidator(SqlDbLanguageSpecifics specifics, IValidatorConnectionFactory cf)
        : base(specifics, cf)
    {
        // Additional custom rules
        RuleFor(e => e.Email).EmailAddress().WhenNotNull();
    }
}
```

### EfValidationErrorCode (enum)

| Value | Description |
|---|---|
| `NullValue` | Non-nullable property is null |
| `StringIsTooLong` | String exceeds column size |
| `NumberIsOutOfRange` | Number exceeds column precision |
| `DateIsOutRange` | Date outside DB range |
| `TimestampIsOutOfRange` | Timestamp outside DB range |
| `EnumerationValueIsInvalid` | Invalid enum value |
| `ValueIsNotUnique` | Unique constraint violation |
| `ReferenceDoesNotExists` | FK reference not found |

### Connection Factories

```csharp
// Creates a new connection for each validation
var factory = new ValidatorConnectionFactory(connectionString, driver);

// Reuses a single connection
var factory = new ValidatorSingletonConnectionFactory(existingConnection);
```

Both implement `IValidatorConnectionFactory`.

### IEfValidatorMessageProvider

```csharp
public interface IEfValidatorMessageProvider
{
    string GetMessage(EntityDescriptor entity, EntityPropertyInfo property, int errorCode);
}
```

Implement this to provide custom/localized error messages for EF validation failures.

---

## Gehtsoft.EF.Mapper.Validator

Bridges mapper and validator: validates model classes against the database constraints of their mapped EF entities using mapping metadata.

### EfModelValidator\<T\>

```csharp
public class Entity2ModelValidator : EfModelValidator<Entity2Model>
{
    public Entity2ModelValidator(SqlDbLanguageSpecifics specifics = null,
                                 IValidatorConnectionFactory connectionFactory = null)
        : base(specifics, connectionFactory)
    {
        // Auto-validate model properties against DB constraints via mapping
        ValidateModel(messageProvider: null);

        // Add custom rules on top
        RuleFor(m => m.StringValue1)
            .NotNullOrWhitespace()
            .WithMessage("Value is required");
    }
}
```

**`ValidateModel()` automatically adds rules for each mapped property**:
- Not null for non-nullable entity columns
- String length check against column size
- Numeric range check against column precision
- Date/timestamp range check (requires `specifics`)
- Uniqueness check (requires `connectionFactory`)
- FK existence check (requires `connectionFactory`)

**Parameters**:
- `messageProvider` (IEfValidatorMessageProvider) - optional custom messages
- `aspNetValidation` (bool) - when true, uses `NotNullOrEmpty` instead of `NotNull` for strings

### Model Validation Attributes

Place these on model properties for attribute-based validation (processed in `EfModelValidator` constructor):

| Attribute | Description |
|---|---|
| `[MustHaveValidDbSize]` | String length must fit DB column size |
| `[MustBeInDbValueRange]` | Numeric/datetime must fit DB column range |
| `[MustBeUnique]` | Value must be unique in mapped DB column |
| `[MustExist]` | Referenced entity must exist in DB |

All attributes support `WidthCode` (int?) and `WithMessage` (string) properties.

```csharp
[MapEntity(EntityType = typeof(MyEntity))]
public class MyModel
{
    [MapProperty]
    [MustHaveValidDbSize]
    [MustBeUnique]
    public string Code { get; set; }

    [MapProperty(Name = nameof(MyEntity.Category))]
    [MustExist]
    public int? CategoryId { get; set; }

    [MapProperty]
    [MustBeInDbValueRange]
    public decimal? Amount { get; set; }
}
```

### Rule Builder Extension Methods

Extension methods available on `GenericValidationRuleBuilder<TE,TV>` and `ValidationRuleBuilder`:

```csharp
// String length against DB column
RuleFor(m => m.Name).MustHaveValidDbSize();

// Numeric/datetime range against DB column
RuleFor(m => m.Amount).MustBeInValidDbRange();

// Uniqueness check
RuleFor(m => m.Code).MustBeUnique();

// FK existence check
RuleFor(m => m.CategoryId).MustExists();
```

**Requirements**:
- `MustHaveValidDbSize` / `MustBeInValidDbRange` - the property must be mapped (via `[MapProperty]`) to an EF entity property. For datetime range validation, `SqlDbLanguageSpecifics` must be passed to the validator constructor.
- `MustBeUnique` / `MustExists` - additionally requires `IValidatorConnectionFactory` in the validator constructor.

---

## Common Patterns

### Full Model Validation Stack

```csharp
// 1. Define entity
[Entity]
public class Product
{
    [EntityProperty(AutoId = true)]
    public int ID { get; set; }

    [EntityProperty(Size = 100, Unique = true)]
    public string Code { get; set; }

    [EntityProperty(Size = 200)]
    public string Name { get; set; }

    [EntityProperty(DbType = DbType.Decimal, Size = 10, Precision = 2)]
    public decimal Price { get; set; }

    [EntityProperty(ForeignKey = true)]
    public Category Category { get; set; }
}

// 2. Define model with mapping
[MapEntity(EntityType = typeof(Product))]
public class ProductModel
{
    [MapProperty]
    public int? ID { get; set; }

    [MapProperty]
    public string Code { get; set; }

    [MapProperty(MapFlags = MapFlag.TrimStrings)]
    public string Name { get; set; }

    [MapProperty]
    public decimal Price { get; set; }

    [MapProperty(Name = nameof(Product.Category))]
    public int? CategoryId { get; set; }
}

// 3. Define validator
public class ProductModelValidator : EfModelValidator<ProductModel>
{
    public ProductModelValidator(SqlDbLanguageSpecifics specifics, IValidatorConnectionFactory cf)
        : base(specifics, cf)
    {
        ValidateModel();

        // Custom business rules
        RuleFor(m => m.Code)
            .DoesMatch(@"^[A-Z]{2}\d{4}$")
            .WithMessage("Code must be 2 letters followed by 4 digits");

        RuleFor(m => m.Price)
            .Must(p => p > 0)
            .WithMessage("Price must be positive");
    }
}

// 4. Use
var validator = new ProductModelValidator(dbSpecifics, connectionFactory);
var result = validator.Validate(model);
```

### Nested Validation

```csharp
public class OrderValidator : AbstractValidator<Order>
{
    public OrderValidator()
    {
        RuleFor(o => o.Customer)
            .NotNull()
            .ValidateUsing<CustomerValidator>();

        RuleForAll(o => o.Items)
            .ValidateUsing<OrderItemValidator>();
    }
}
// Failures will have paths like "Customer.Name", "Items[0].Quantity"
```

### Conditional Validation

```csharp
public class AccountValidator : AbstractValidator<Account>
{
    public AccountValidator()
    {
        // Different rules based on account type
        RuleFor(a => a.CompanyName)
            .WhenEntity(a => a.Type == AccountType.Business)
            .NotNullOrWhitespace();

        RuleFor(a => a.FirstName)
            .WhenEntity(a => a.Type == AccountType.Personal)
            .NotNullOrWhitespace();

        // Rule with otherwise
        RuleFor(a => a.TaxId)
            .WhenEntity(a => a.Country == "US")
            .DoesMatch(@"^\d{2}-\d{7}$")
            .WithMessage("EIN format required")
            .Otherwise()
            .NotNullOrWhitespace()
            .WithMessage("Tax ID required");
    }
}
```

### Server-Only Rules

```csharp
RuleFor(p => p.Code)
    .MustBeUnique()         // DB check - cannot be in JS
    .ServerOnly();          // explicitly mark

RuleFor(p => p.Name)
    .NotNullOrWhitespace()  // this CAN be compiled to JS
    .ShorterThan(101);      // this CAN be compiled to JS
```
