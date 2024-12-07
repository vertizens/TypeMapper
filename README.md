# Vertizens.TypeMapper

C# Type Mapper that uses name matching conventions but still allows for customization

## Getting Started

Dependency injection
usage:
```
services.AddTypeMappers();
```

## ITypeMapper and ITypeMapper<TSource, TTarget>

This is the main interface you want to inject into your code.  
Map from one type to another if the target type has an empty public constructor.
```
var target = mapper.Map<SourceType,TargetType>(source);
```

Or map onto an existing object.
```
mapper.Map(source, target);
```
Since ITypeMapper use IServiceProvider you may want to be explicit it the mapper you use and instead use ITypeMapper<TSource, TTarget>.  But it only maps from one existing instance to another.

```
mapper.Map(source, target);
```

## Default Name Match Mapping

By default, public properties with the same name and equivalent types are matched.  First it checks if they are Assignable with Type.IsAssignableFrom method.  A nullable int32 is not the same as int32, it can only go from int to nullable int and not the other way around.  Other than nullability assume the properties have to be the exact same name and exact same type.  If not, its not getting mapped.  
Next is the properties are not the same type but both are classes.  This is where the type mapping in a sense gets recursive as the process starts all over but with the two property types in question.
Finally, if the source is IEnumerable<> and the target is assignable by a List<> then it will also be mapped.  But only if they are generics of a type that can be mapped.

## ITypeMapperBuilder<TSource, TTarget>

Implement your own custom logic for mapping one type onto another.  Any type that implements this interface gets registered with the `AddTypeMappers` method call.  The `Build` method gets
called with an instance of `ITypeMapperExpressionBuilder<TSource, TTarget>`.  With this you 
can call `ApplyNameMatch()` or any number of `Map<TProperty>()` calls.  Call the `ApplyNameMatch` first if required then any custom property mappings by defining a property selection and value selection expressions.

```
public void Build(ITypeMapperExpressionBuilder<SourceType, TargetType> expressionBuilder)
{
    expressionBuilder
        .ApplyNameMatch()
        .Map(t => t.TargetProperty, s => s.SourceValue);
}
```

## ITypeProjector<TSource, TTarget>

This is very similar to a mapper but this is used for projecting one type to another.  Useful for when there are not existing instances and they will be created with initialization.  The main use case is for Linq to Sql where an expression is used to project from an entity to another type as part of the select and actually alter what SQL is produced as part of a query.
Inject `ITypeProjector<TSource, TTarget>` to be able to call `GetProjection()` which return 
`Expression<Func<TSource, TTarget>>` which is used for a `Select` part of an `IQueryable`.
To customize follow this pattern:

```
internal class SourceToTargetProjector(
    ITypeProjectorExpressionBuilder<Source, Target> _expressionBuilder
    ) : ITypeProjector<Source, Target>
{
    public Expression<Func<Source, Target>> GetProjection()
    {
        return _expressionBuilder
            .ApplyNameMatch() //if applicable
            .Union(s => new Target { TargetProperty = s.SourceProperty })
            .Build();
    }
}
```

Since it just uses expressions you could forego the usage of `ITypeProjectorExpressionBuilder` and just perform all the projection manually here instead'

```
internal class SourceToTargetProjector : ITypeProjector<Source, Target>
{
    public Expression<Func<Source, Target>> GetProjection()
    {
        return (s,t) => new Target  { TargetProperty = s.SourceProperty };
    }
}
```

## Note on ITypeMapperBuilder<TSource, TTarget> and ITypeProjector<TSource, TTarget>

If you add custom code for `ITypeProjector<TSource, TTarget>` then `ITypeMapperBuilder<TSource, TTarget>` will use any custom implemention from the projector unless you create your own implementation for that too.

## Use Case Suggestions

This is intended to not have a lot of rules that convert types, not even primitives like int32 to int64.  If types are not exact that hold values then custom is required.  This utility is intended where one type is essentially a subset of another and lends itself to low effort in accomplishing that task.  Always check that a target type has mapped from the source type as desired.
