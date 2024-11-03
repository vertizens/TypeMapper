using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace Vertizens.TypeMapper;
internal class NameMatchTypeMapper<TSource, TTarget> : INameMatchTypeMapper<TSource, TTarget>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IList<AssignablePropertyContext> _assignablePropertyContexts = [];
    private readonly IList<ClassPropertyContext> _classPropertyContexts = [];
    private readonly IList<GenericEnumerablePropertyContext> _genericEnumerablePropertyContexts = [];

    private class AssignablePropertyContext
    {
        public required PropertyInfo TargetSet;
        public required PropertyInfo SourceGet;
        public required Action<TSource, TTarget> SetAction;
    }

    private class ClassPropertyContext
    {
        public required PropertyInfo TargetSet;
        public required PropertyInfo SourceGet;
        public required object TypeMapper;
        public required MethodInfo MapMethod;
    }

    private class GenericEnumerablePropertyContext
    {
        public required PropertyInfo TargetSet;
        public required PropertyInfo SourceGet;
        public required object TypeMapper;
        public required MethodInfo MapMethod;
        public required Type TargetGenericType;
        public required Type TargetGenericListType;
        public required MethodInfo ListAddMethod;
    }

    public NameMatchTypeMapper(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        BuildPropertyContexts();
    }

    public void Map(TSource sourceObject, TTarget targetObject)
    {
        if (sourceObject == null)
        {
            throw new ArgumentNullException(nameof(sourceObject));
        }

        if (targetObject == null)
        {
            throw new ArgumentNullException(nameof(targetObject));
        }

        foreach (var assignablePropertyContext in _assignablePropertyContexts)
        {
            assignablePropertyContext.SetAction(sourceObject, targetObject);
        }

        foreach (var classPropertyContext in _classPropertyContexts)
        {
            MapClassProperty(sourceObject, targetObject, classPropertyContext);
        }

        foreach (var genericEnumerablePropertyContext in _genericEnumerablePropertyContexts)
        {
            MapGenericEnumerableProperty(sourceObject, targetObject, genericEnumerablePropertyContext);
        }
    }

    private void BuildPropertyContexts()
    {
        var sourceGetProperties = typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.GetMethod?.IsPublic == true);
        var targetSetProperties = typeof(TTarget).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.SetMethod?.IsPublic == true).ToDictionary(x => x.Name);
        foreach (var sourceGetProperty in sourceGetProperties)
        {
            if (targetSetProperties.TryGetValue(sourceGetProperty.Name, out var targetSetProperty))
            {
                BuildPropertyContext(sourceGetProperty, targetSetProperty);
            }
        }
    }

    private void BuildPropertyContext(PropertyInfo sourceGetProperty, PropertyInfo targetSetProperty)
    {

        if (targetSetProperty.PropertyType.IsAssignableFrom(sourceGetProperty.PropertyType))
        {
            BuildAssignablePropertyContext(sourceGetProperty, targetSetProperty);
        }
        else if (targetSetProperty.PropertyType.IsClass && sourceGetProperty.PropertyType.IsClass && sourceGetProperty.PropertyType != typeof(string))
        {
            BuildClassPropertyContext(sourceGetProperty, targetSetProperty);
        }
        else if (targetSetProperty.PropertyType.IsConstructedGenericType && sourceGetProperty.PropertyType.IsConstructedGenericType)
        {
            BuildGenericEnumerablePropertyContext(sourceGetProperty, targetSetProperty);
        }
    }

    private void BuildAssignablePropertyContext(PropertyInfo sourceGetProperty, PropertyInfo targetSetProperty)
    {

        var parameterSource = Expression.Parameter(typeof(TSource), "sourceObject");
        var parameterTarget = Expression.Parameter(typeof(TTarget), "targetObject");
        Expression sourceProperty = Expression.Property(parameterSource, sourceGetProperty);
        var targetProperty = Expression.Property(parameterTarget, targetSetProperty);
        if (sourceGetProperty.PropertyType != targetSetProperty.PropertyType)
        {
            sourceProperty = Expression.ConvertChecked(sourceProperty, targetSetProperty.PropertyType);
        }
        var assignment = Expression.Assign(targetProperty, sourceProperty);
        var expression = Expression.Lambda<Action<TSource, TTarget>>(assignment, parameterSource, parameterTarget);
        var action = expression.Compile();

        _assignablePropertyContexts.Add(new AssignablePropertyContext { TargetSet = targetSetProperty, SourceGet = sourceGetProperty, SetAction = action });
    }

    private void BuildClassPropertyContext(PropertyInfo sourceGetProperty, PropertyInfo targetSetProperty)
    {
        var constructor = targetSetProperty.PropertyType.GetConstructor(BindingFlags.Public | BindingFlags.Instance, []);
        if (constructor != null)
        {
            var genericTypeMapperType = typeof(ITypeMapper<,>).MakeGenericType(sourceGetProperty.PropertyType, targetSetProperty.PropertyType);
            var genericTypeMapper = _serviceProvider.GetService(genericTypeMapperType);
            if (genericTypeMapper != null)
            {
                var method = genericTypeMapperType.GetMethod(nameof(this.Map), BindingFlags.Public | BindingFlags.Instance);
                var context = new ClassPropertyContext
                {
                    TargetSet = targetSetProperty,
                    SourceGet = sourceGetProperty,
                    TypeMapper = genericTypeMapper,
                    MapMethod = method!
                };
                _classPropertyContexts.Add(context);
            }
        }
    }

    private void BuildGenericEnumerablePropertyContext(PropertyInfo sourceGetProperty, PropertyInfo targetSetProperty)
    {
        var targetGenericArguments = targetSetProperty.PropertyType.GetGenericArguments();
        var listType = typeof(List<>).MakeGenericType(targetGenericArguments);

        if (targetSetProperty.PropertyType.IsAssignableFrom(listType) && sourceGetProperty.PropertyType.IsAssignableTo(typeof(IEnumerable)))
        {
            var targetGenericType = targetGenericArguments.First();
            var sourceGenericType = sourceGetProperty.PropertyType.GetGenericArguments().First();

            var constructor = targetGenericType.GetConstructor(BindingFlags.Public | BindingFlags.Instance, []);
            if (constructor != null)
            {
                var genericTypeMapperType = typeof(ITypeMapper<,>).MakeGenericType(sourceGenericType, targetGenericType);
                var propertyTypeMapper = _serviceProvider.GetService(genericTypeMapperType);

                if (propertyTypeMapper != null)
                {
                    var mapMethod = genericTypeMapperType.GetMethod(nameof(this.Map));
                    var targetGenericList = typeof(List<>).MakeGenericType(targetGenericType);
                    var addMethod = targetGenericList.GetMethod(nameof(List<object>.Add), BindingFlags.Public | BindingFlags.Instance);

                    var context = new GenericEnumerablePropertyContext
                    {
                        TargetSet = targetSetProperty,
                        SourceGet = sourceGetProperty,
                        TypeMapper = propertyTypeMapper,
                        MapMethod = mapMethod!,
                        TargetGenericType = targetGenericType,
                        TargetGenericListType = targetGenericList,
                        ListAddMethod = addMethod!
                    };
                    _genericEnumerablePropertyContexts.Add(context);
                }
            }
        }
    }

    private static void MapClassProperty(TSource sourceObject, TTarget targetObject, ClassPropertyContext context)
    {
        var sourcePropertyValue = context.SourceGet.GetValue(sourceObject);
        if (sourcePropertyValue != null)
        {
            var targetPropertyInstance = Activator.CreateInstance(context.TargetSet.PropertyType);
            context.MapMethod.Invoke(context.TypeMapper, [sourcePropertyValue, targetPropertyInstance]);

            context.TargetSet.SetValue(targetObject, targetPropertyInstance);
        }
        else
        {
            context.TargetSet.SetValue(targetObject, null);
        }
    }

    private static void MapGenericEnumerableProperty(TSource sourceObject, TTarget targetObject, GenericEnumerablePropertyContext context)
    {
        object? targetPropertyList = null;
        var sourcePropertyValues = (IEnumerable?)context.SourceGet.GetValue(sourceObject);
        if (sourcePropertyValues != null)
        {
            targetPropertyList = MapSourceList(sourcePropertyValues, context);
        }
        context.TargetSet.SetValue(targetObject, targetPropertyList);
    }

    private static object? MapSourceList(IEnumerable sourcePropertyValues, GenericEnumerablePropertyContext context)
    {
        var targetPropertyList = Activator.CreateInstance(context.TargetGenericListType);
        foreach (var sourcePropertyValue in sourcePropertyValues)
        {
            object? targetPropertyInstance = null;
            if (sourcePropertyValue != null)
            {
                targetPropertyInstance = Activator.CreateInstance(context.TargetGenericType);
                context.MapMethod.Invoke(context.TypeMapper, [sourcePropertyValue, targetPropertyInstance]);
            }
            context.ListAddMethod!.Invoke(targetPropertyList, [targetPropertyInstance]);
        }

        return targetPropertyList;
    }
}
