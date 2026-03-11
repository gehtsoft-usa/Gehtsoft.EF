# SKILL: Gehtsoft.Mapper / Gehtsoft.EF.Mapper

## Overview

Object-to-object mapping library for .NET Standard 2.0. All classes are in the `Gehtsoft.EF.Mapper` namespace.

- **Gehtsoft.Mapper** - generic mapping between any two classes (no EF dependency).
- **Gehtsoft.EF.Mapper** - extends Gehtsoft.Mapper with EF entity awareness via `[MapEntity]` attribute and `EntityPropertyAccessor`. Depends on `Gehtsoft.EF.Db.SqlDb`.

Use `Gehtsoft.Mapper` when no EF entities are involved; use `Gehtsoft.EF.Mapper` otherwise.

## NuGet Packages

| Package | Dependencies |
|---|---|
| `Gehtsoft.Mapper` | `Gehtsoft.Tools2` |
| `Gehtsoft.EF.Mapper` | `Gehtsoft.Mapper`, `Gehtsoft.EF.Db.SqlDb` |

## Core Types

### MapFactory (static class)

Central entry point for all mapping operations.

```csharp
// Create and register a map (do once at startup)
Map<TFrom, TTo> map = MapFactory.CreateMap<TFrom, TTo>();

// Get existing map (or auto-create if possible)
Map<TFrom, TTo> map = MapFactory.GetMap<TFrom, TTo>(createIfNotExist: true);

// Check if map exists
bool exists = MapFactory.HasMap<TFrom, TTo>();

// One-shot mapping (auto-creates map if needed)
TTo result = MapFactory.Map<TFrom, TTo>(source);
MapFactory.Map<TFrom, TTo>(source, existingDestination);

// Remove a registered map
MapFactory.RemoveMap<TFrom, TTo>();
```

**Auto-creation**: `CreateMap` and `GetMap` will automatically initialize the map if either the source or destination type has a `[MapEntity]` or `[MapClass]` attribute pointing to the other type.

**Collection support**: `MapFactory.Map` handles arrays, `IList`, `IEnumerable` automatically when element maps exist.

### Map<TSource, TDestination>

Defines how to map properties from source to destination.

```csharp
// Property mapping (fluent)
map.For(d => d.DestProp)           // select destination property
   .From(s => s.SourceProp);       // set source property

map.For("PropertyName")            // by string name
   .From("SourcePropertyName");

// Constant / computed assignment
map.For(d => d.Prop).Assign(constantValue);
map.For(d => d.Prop).Assign(source => ComputeValue(source));

// Conditional mapping
map.For(d => d.Prop).From(s => s.Val)
   .When(s => s.Val > 0);                    // source predicate
map.For(d => d.Prop).From(s => s.Val)
   .WhenDestination(d => d.OtherProp != null); // destination predicate

// Conditional branching
map.For(d => d.Prop).From(s => s.A)
   .When(s => s.UseA)
   .Otherwise()                               // opposite condition
   .From(s => s.B);

// Ignore a property
map.For(d => d.Prop).Ignore();
map.Find(d => d.Prop).Ignore();  // ignore all rules for this property

// Replace existing rules
map.For(d => d.Prop).ReplaceWith().From(s => s.NewSource);

// Mapping flags
map.For(d => d.Prop).From(s => s.Val).WithFlags(MapFlag.TrimStrings);

// Before/After hooks
map.BeforeMapping((source, dest) => { /* init */ });
map.AfterMapping((source, dest) => { /* finalize */ });
map.BeforeMapping((s, d) => d.Prop = 0).When((s, d) => condition);

// Auto-map by property name
map.MapPropertiesByName(
    onlyValueTypes: false,
    propertyIgnoreList: new[] { "IgnoreThis" },
    typeIgnoreList: new[] { typeof(SomeType) }
);

// Custom destination factory
map.Factory = source => new TDest(source.Id);

// Execute mapping
TDest result = map.Do(source);                    // create new destination
map.Do(source, existingDest);                     // map into existing
map.Do(source, existingDest, ignoreNull: true);   // skip null source values

// MapNullToNull (default: true) - if source is null, return default(TDest)
map.MapNullToNull = false;
```

### PropertyMapping<TSource, TDestination>

Returned by `map.For(...)`. Fluent API:

| Method | Description |
|---|---|
| `.From(s => s.Prop)` | Set source from property expression |
| `.From("name")` | Set source from property name |
| `.Assign(value)` | Set constant value |
| `.Assign(s => expr)` | Set computed value |
| `.When(s => predicate)` | Conditional on source |
| `.WhenDestination(d => pred)` | Conditional on destination |
| `.Otherwise()` | New rule with opposite condition |
| `.Always()` | Remove condition |
| `.Ignore()` | Never execute this rule |
| `.ReplaceWith()` | Ignore all previous rules for same target, create new |
| `.WithFlags(MapFlag)` | Apply mapping flags |

### MapFlag (enum, Flags)

| Value | Effect |
|---|---|
| `None` | Default behavior |
| `TrimStrings` | `string.Trim()` on string values |
| `TrimToSeconds` | Truncate DateTime to whole seconds |
| `TrimToDate` | Truncate DateTime to date only |

## Attribute-Based Mapping

### Entity-to-Model (requires Gehtsoft.EF.Mapper)

```csharp
[MapEntity(EntityType = typeof(MyEntity))]
public class MyModel
{
    [MapProperty]                                      // same name as entity property
    public int? ID { get; set; }

    [MapProperty(Name = nameof(MyEntity.FkField))]     // different name
    public int? ForeignKeyId { get; set; }

    [MapProperty(MapFlags = MapFlag.TrimStrings)]       // with flags
    public string Name { get; set; }

    [MapProperty(IgnoreToModel = true)]                 // skip when mapping entity -> model
    public string WriteOnly { get; set; }

    [MapProperty(IgnoreFromModel = true)]               // skip when mapping model -> entity
    public string ReadOnly { get; set; }
}
```

`MapEntityAttribute` properties:
- `EntityType` (Type) - the EF entity type this model maps to.

### Class-to-Model (Gehtsoft.Mapper only)

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

`MapClassAttribute` properties:
- `OtherType` (Type) - the other class type.

### MapPropertyAttribute Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `Name` | string | null | Source property name (null = same name as model property) |
| `MapFlags` | MapFlag | None | Mapping behavior flags |
| `IgnoreToModel` | bool | false | Skip when mapping source -> model |
| `IgnoreFromModel` | bool | false | Skip when mapping model -> source |

### DoNotAutoMapAttribute

Place on a property to exclude it from all automatic mapping (`MapPropertiesByName`, attribute-based auto-init).

```csharp
[DoNotAutoMap]
public string InternalField { get; set; }
```

## Value Mapping Rules

`ValueMapper.MapValue()` (used internally by `MapFactory.Map`) handles type conversion:

1. **Same type, value/string/enum** - returns value as-is.
2. **Same type, map exists** - uses the registered map.
3. **Same type, no map** - returns source reference.
4. **Destination is value type or string** - uses `Convert.ChangeType` (InvariantCulture).
5. **Destination is enum** - parses from string or converts from int.
6. **Array to array** - maps each element recursively.
7. **IEnumerable to array** - maps each element recursively.
8. **Array/IEnumerable to IList** - maps each element, adds to new list.
9. **Other** - looks up or auto-creates map; falls back to assignability check.

## EF-Specific Features (Gehtsoft.EF.Mapper)

### EntityPropertyAccessor

When mapping EF entities, the mapper uses `EntityPropertyAccessor` instead of `ClassPropertyAccessor`. This preserves EF metadata (column info, FK info, etc.) on each mapping rule, which is used by `Gehtsoft.EF.Mapper.Validator` for automatic DB constraint validation.

### EntityPrimaryKeySource / ModelPrimaryKeySource

Special source accessors for foreign key mapping:
- When mapping entity -> model, FK entity references are automatically resolved to their primary key value.
- When mapping model -> model, primary key values are resolved back to entity references.

## Common Patterns

### Startup Registration

```csharp
// Application startup
var map = MapFactory.CreateMap<Entity, EntityModel>();
// Customize if needed:
map.Find(d => d.ComputedField).Ignore();
map.For(d => d.DisplayName).Assign(s => $"{s.First} {s.Last}");

var reverseMap = MapFactory.CreateMap<EntityModel, Entity>();
```

### Mapping with Null Handling

```csharp
var map = MapFactory.GetMap<Model, Entity>();
// Update entity from model, keeping existing values where model is null
map.Do(model, existingEntity, ignoreNull: true);
```

### Batch Mapping

```csharp
Entity[] entities = GetEntities();
EntityModel[] models = MapFactory.Map<Entity[], EntityModel[]>(entities);
List<EntityModel> modelList = MapFactory.Map<Entity[], List<EntityModel>>(entities);
```
