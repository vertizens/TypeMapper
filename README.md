# Vertizens.TypeMapper

C# Type Mapper that uses name matching conventions but still allows for customization

## Getting Started

Dependency injection
usage:
```
services.AddTypeMappers();
```

## ITypeMapper

This is the main interface you want to inject into your code.  
Map from one type to another if the target type has an empty public constructor.
```
var target = mapper.Map<SourceType,TargetType>(source);
```

Or map onto an existing object.
```
mapper.Map(source, target);
```

## Default Name Match Mapping

By default, public properties with the same name and equivalent types are matched.  First it checks if they are Assignable with Type.IsAssignableFrom method.  A nullable int32 is not the same as int32, it can only go from int to nullable int and not the other way around.  Other than nullability assume the properties have to be the exact same name and exact same type.  If not, its not getting mapped.  
Next is the properties are not the same type but both are classes.  This is where the type mapping in a sense gets recursive as the process starts all over but with the two property types in question.
Finally, if the source is IEnumerable<> and the target is assignable by a List<> then it will also be mapped.  But only if they are generics of a type that can be mapped.

## ITypeMapper<TSource, TTarget>

Implement your own custom logic for mapping one type onto another.  Then register in your DI container
and it will get used by ITypeMapper.  If you need the default Name Matching specifically for two types then inject INameMatchTypeMapper<Type1,Type2> for each one you need.  You can also do it for the same type you are custom mapping so you can get the default behavior first then override or do mapping it missed.  Likewise, inject IITypeMapper<TType1, Type2> to get whatever possible custom behavior you need for child property objects.

## INameMatchTypeMapper<TSource,TTarget>

Just remember that ITypeMapper<TSource, TTarget> includes any customization that you write where as INameMatchTypeMapper<TSource,TTarget> maps based on the default name/type matching.

## Use Case Suggestions

This is intended to not have a lot of rules that convert types, not even primitives like int32 to int64.  If types are not exact that hold values then custom is required.  This utility is intended where one type is essentially a subset of another and lends itself to low effort in accomplishing that task.  Always check that a target type has mapped from the source type as desired.
