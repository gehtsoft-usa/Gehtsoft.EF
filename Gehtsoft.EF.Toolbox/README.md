# Gehtsoft EF Toolbox: Mapper and Validator Libraries

A set of .NET Standard 2.0 libraries for mapping between objects and validating them, with deep integration with the Gehtsoft.EF entity framework.

## Projects Overview

| Package | Purpose | Key Dependencies |
|---|---|---|
| **Gehtsoft.Mapper** | Generic object-to-object mapping | Gehtsoft.Tools2 |
| **Gehtsoft.EF.Mapper** | Mapping between EF entities and model classes | Gehtsoft.Mapper, Gehtsoft.EF.Db.SqlDb |
| **Gehtsoft.Validator** | Generic object validation framework | Gehtsoft.ExpressionToJs |
| **Gehtsoft.EF.Validator** | Automatic validation of EF entities based on entity metadata | Gehtsoft.Validator, Gehtsoft.EF.Db.SqlDb |
| **Gehtsoft.EF.Mapper.Validator** | Validation of mapper model classes against their associated EF entities | Gehtsoft.EF.Mapper, Gehtsoft.EF.Validator |

## Gehtsoft.Mapper

A lightweight object-to-object mapping library. Use `Gehtsoft.Mapper` when you do not need EF entity support; use `Gehtsoft.EF.Mapper` when mapping involves EF entities. All classes reside in the `Gehtsoft.EF.Mapper` namespace.

### Three Ways to Define a Map

#### 1. Attribute-based mapping from an EF entity to a model

Decorate your model class with `[MapEntity]` and each mapped property with `[MapProperty]`:

```csharp
[Entity]
public class Entity2
{
    [EntityProperty(AutoId = true)]
    public int ID { get; set; }

    [EntityProperty(ForeignKey = true)]
    public Entity1 Entity1 { get; set; }

    [EntityProperty]
    public string StringValue1 { get; set; }

    [EntityProperty]
    public int IntegerValue { get; set; }

    [EntityProperty(DbType = DbType.Int32, Nullable = true)]
    public EntityEnum? EnumValue { get; set; }
}

[MapEntity(EntityType = typeof(Entity2))]
public class Entity2Model
{
    [MapProperty]
    public int? ID { get; set; }

    [MapProperty(Name = nameof(Entity2.Entity1))]
    public int? Reference { get; set; }

    [MapProperty(MapFlags = MapFlag.TrimStrings)]
    public string StringValue1 { get; set; }

    [MapProperty]
    public decimal IntegerValue { get; set; }

    [MapProperty]
    public int? EnumValue { get; set; }
}

// Map an entity to the model:
var model = MapFactory.Map<Entity2, Entity2Model>(entity);
```

#### 2. Attribute-based mapping between two arbitrary classes

Decorate the model class with `[MapClass]` and properties with `[MapProperty]`:

```csharp
[MapClass(typeof(SourceClass))]
public class DestModel
{
    [MapProperty]
    public int ID { get; set; }

    [MapProperty(Name = "OriginalName")]
    public string RenamedProp { get; set; }
}
```

#### 3. Fluent (custom) mapping

Create the map manually and define rules with a fluent API:

```csharp
// Create map (do this once at startup)
Map<Class1, Class2> map = MapFactory.CreateMap<Class1, Class2>();
map.For(d => d.ID).From(s => s.ID);
map.For(d => d.Title).From(s => s.Name).WithFlags(MapFlag.TrimStrings);
map.For(d => d.DoubleValue).From(s => s.IntegerValue);

Map<Class2, Class1> reverse = MapFactory.CreateMap<Class2, Class1>();
reverse.For(d => d.ID).From(s => s.ID);
reverse.For(d => d.Name).From(s => s.Title);
reverse.For(d => d.IntegerValue).From(s => s.DoubleValue)
    .When(s => s.DoubleValue >= Int32.MinValue && s.DoubleValue <= Int32.MaxValue);
reverse.BeforeMapping((s, d) => d.IntegerValue = 0);

// Use the map
Class2 result = MapFactory.Map<Class1, Class2>(source);
Class2[] array = MapFactory.Map<List<Class1>, Class2[]>(list);
```

### Key API

- **`MapFactory.Map<TFrom, TTo>(source)`** - performs one-shot mapping (creates map automatically if possible).
- **`MapFactory.CreateMap<TFrom, TTo>()`** - creates and registers a map for later use.
- **`MapFactory.GetMap<TFrom, TTo>()`** - retrieves an existing map (or creates one if possible).
- **`Map<TS, TD>.For(d => d.Prop)`** - starts a property mapping rule for a destination property.
- **`PropertyMapping.From(s => s.Prop)`** - sets the source of the mapping.
- **`PropertyMapping.Assign(value)`** - assigns a constant or computed value.
- **`PropertyMapping.When(predicate)`** - adds a condition under which the mapping applies.
- **`PropertyMapping.Otherwise()`** - creates a rule with the opposite condition.
- **`PropertyMapping.WithFlags(MapFlag)`** - applies flags like `TrimStrings`, `TrimToSeconds`, `TrimToDate`.
- **`PropertyMapping.Ignore()`** - suppresses a mapping rule.
- **`Map.BeforeMapping(action)` / `Map.AfterMapping(action)`** - hooks that run before/after mapping.
- **`Map.Do(source)` / `Map.Do(source, destination, ignoreNull)`** - performs the mapping with optional null-skip.
- **`Map.MapPropertiesByName()`** - auto-maps properties by matching names.
- **`[DoNotAutoMap]`** - excludes a property from automatic mapping.

## Gehtsoft.Validator

A fluent, rule-based object validation framework. Validators are defined by deriving from `AbstractValidator<T>` and declaring rules in the constructor.

### Basic Usage

```csharp
public class PersonValidator : AbstractValidator<Person>
{
    public PersonValidator()
    {
        RuleFor(p => p.Name)
            .NotNullOrWhitespace()
            .ShorterThan(101)
            .WithMessage("Name is required and must be 100 chars or less");

        RuleFor(p => p.Email)
            .WhenNotNull()
            .EmailAddress()
            .WithCode(1001);

        RuleFor(p => p.Age)
            .Must(age => age >= 0 && age <= 150)
            .WithMessage("Age must be between 0 and 150");

        RuleFor(p => p.PhoneNumber)
            .WhenNotNull()
            .DoesMatch(@"^\+?\d{7,15}$");
    }
}

// Validate
var validator = new PersonValidator();
ValidationResult result = validator.Validate(person);

if (!result.IsValid)
{
    foreach (ValidationFailure failure in result.Failures)
        Console.WriteLine($"{failure.Path}: {failure.Message} (code: {failure.Code})");
}
```

### Built-in Predicates

| Method | Description |
|---|---|
| `NotNull()` | Value must not be null |
| `Null()` | Value must be null |
| `NotNullOrEmpty()` | Value must not be null or empty (strings, arrays, collections) |
| `NotNullOrWhitespace()` | Value must not be null, empty, or whitespace |
| `ShorterThan(n)` | String/collection length must be less than `n` |
| `DoesMatch(pattern)` | String must match the regex pattern |
| `DoesNotMatch(pattern)` | String must not match the regex pattern |
| `Between(min, max)` | Value must be within range (inclusive by default) |
| `EnumIsCorrect()` | Enum value must be a defined member |
| `EmailAddress()` | Must be a valid email address |
| `PhoneNumber()` | Must be a valid phone number |
| `CreditCardNumber()` | Must pass Luhn check |
| `NotSQLInjection()` | Must not contain SQL injection patterns |
| `NotHTML()` | Must not contain HTML injection patterns |
| `Must(expression)` | Custom LINQ expression predicate |

### Rule Modifiers

- **`WhenValue(predicate)` / `UnlessValue(predicate)`** - apply the rule only when/unless the value matches.
- **`WhenEntity(predicate)` / `UnlessEntity(predicate)`** - apply the rule only when/unless the whole entity matches.
- **`WhenNotNull()`** - skip the rule if the value is null.
- **`WithCode(int)` / `WithMessage(string)`** - set error code and message on the failure.
- **`ValidateUsing(typeof(OtherValidator))` / `ValidateUsing(validatorInstance)`** - delegate validation to another validator; failure paths are composed (e.g., `Address.City`).
- **`ServerOnly()`** - exclude the rule from JavaScript compilation.
- **`Otherwise()`** - create a new rule with the opposite condition.
- **`Also()`** - create a new rule with the same target and condition.

### Attribute-based Validation

Properties can also be decorated with validation attributes:

- `[MustBeNotNull]`
- `[MustBeNotNullOrWhitespace]`
- `[MustBeShorterThan(length)]`
- `[MustBeInRange(min, max)]`
- `[MustMatch(pattern)]`
- `[MustBeNotEmpty]`

### JavaScript Compilation

Validator rules can be compiled to JavaScript for client-side validation using the `Gehtsoft.Validator.JSConvertor` namespace. This requires the `Gehtsoft.ExpressionToJs` package. Not all predicates can be compiled - function-based, enum validity, uniqueness, and existence checks are server-only. Rules can be explicitly marked with `.ServerOnly()` to prevent JavaScript compilation.

## Gehtsoft.EF.Validator

Provides `EfEntityValidator<T>`, a validator that automatically creates validation rules from EF entity metadata. It checks:

- Non-nullable properties are not null
- Strings do not exceed the column size limit
- Numeric values fit within column precision
- Dates/timestamps are within database-specific ranges
- Unique column values are actually unique (requires DB connection)
- Foreign key references exist (requires DB connection)

### Usage

```csharp
var validator = new EfEntityValidator<MyEntity>(
    specifics: connection.GetLanguageSpecifics(),   // optional: for date range checks
    connectionFactory: new ValidatorConnectionFactory(connectionString, driver)  // optional: for uniqueness/FK checks
);

ValidationResult result = validator.Validate(entity);
```

Error codes are defined in `EfValidationErrorCode`:
- `NullValue`
- `StringIsTooLong`
- `NumberIsOutOfRange`
- `DateIsOutRange`
- `TimestampIsOutOfRange`
- `EnumerationValueIsInvalid`
- `ValueIsNotUnique`
- `ReferenceDoesNotExists`

### Connection Factories

Two built-in implementations of `IValidatorConnectionFactory`:
- **`ValidatorConnectionFactory`** - creates a new connection for each validation.
- **`ValidatorSingletonConnectionFactory`** - reuses a single connection.

## Gehtsoft.EF.Mapper.Validator

Bridges the mapper and validator: validates model classes against the database constraints of their mapped EF entities. This is the most powerful combination - you define models with `[MapEntity]`/`[MapProperty]` attributes and get automatic DB-constraint validation without writing any validation rules.

### Usage

```csharp
public class Entity2ModelValidator : EfModelValidator<Entity2Model>
{
    public Entity2ModelValidator()
        : base(specifics: myDbSpecifics, connectionFactory: myConnectionFactory)
    {
        // Automatically validates all mapped properties against DB constraints
        ValidateModel(messageProvider: null);

        // Add custom rules on top
        RuleFor(m => m.StringValue1)
            .NotNullOrWhitespace()
            .WithMessage("Value is required");
    }
}
```

### Model Validation Attributes

These attributes can be placed on model properties to add DB-aware validation:

| Attribute | Description |
|---|---|
| `[MustBeUnique]` | Checks uniqueness against the mapped DB column |
| `[MustExist]` | Checks that the referenced entity exists in the database |
| `[MustBeInDbValueRange]` | Checks that numeric/datetime values fit the DB column range |
| `[MustHaveValidDbSize]` | Checks that string length fits the DB column size |

### Rule Builder Extensions

Extension methods for the fluent rule builder:

```csharp
RuleFor(m => m.Name).MustHaveValidDbSize();
RuleFor(m => m.Amount).MustBeInValidDbRange();
RuleFor(m => m.Code).MustBeUnqiue();
RuleFor(m => m.CategoryId).MustExists();
```

## Architecture

```
Gehtsoft.Mapper (generic mapping)
    |
    v
Gehtsoft.EF.Mapper (EF entity mapping)
    |                                       Gehtsoft.Validator (generic validation)
    |                                           |
    |                                           v
    |                                       Gehtsoft.EF.Validator (EF entity validation)
    |                                           |
    +-------------------------------------------+
    |
    v
Gehtsoft.EF.Mapper.Validator (model validation against DB constraints via mapping)
```

## Target Framework

All libraries target **.NET Standard 2.0**, making them compatible with .NET Framework 4.6.1+, .NET Core 2.0+, and .NET 5+.
