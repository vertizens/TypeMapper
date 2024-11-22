using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace Vertizens.TypeMapper;
internal class DefaultTypeMapperExpressionBuilder<TSource, TTarget>(
    IServiceProvider _serviceProvider
    ) : ITypeMapperExpressionBuilder<TSource, TTarget>
{
    private readonly ParameterExpression parameterSource = Expression.Parameter(typeof(TSource), "source");
    private readonly ParameterExpression parameterTarget = Expression.Parameter(typeof(TTarget), "target");
    private readonly IList<Expression> _expressions = [];

    public ITypeMapperExpressionBuilder<TSource, TTarget> Map<T>(Expression<Func<TTarget, T>> propertySelector, Expression<Func<TSource, T>> valueSelector)
    {
        var propertyExpression = ReplaceParameterExpressionVisitor.ReplaceParameter(propertySelector.Body, propertySelector.Parameters[0], parameterTarget);
        var valueExpression = ReplaceParameterExpressionVisitor.ReplaceParameter(valueSelector.Body, valueSelector.Parameters[0], parameterSource);
        var propertyAssignment = Expression.Assign(propertyExpression, valueExpression);
        _expressions.Add(propertyAssignment);

        return this;
    }

    //public ITypeMapperExpressionBuilder<TSource, TTarget> Map<TProperty, TMappedProperty>(Expression<Func<TTarget, TProperty>> propertySelector, Expression<Func<TSource, TMappedProperty>> valueSelector)
    //    where TProperty : class
    //    where TMappedProperty : class
    //{
    //    throw new NotImplementedException();
    //    //    var propertyExpressionBuilder = new DefaultTypeMapperExpressionBuilder<TMappedProperty, TProperty>(_serviceProvider);
    //    //    var mapperBuilder = _serviceProvider.GetRequiredService<ITypeMapperBuilder<TMappedProperty, TProperty>>();
    //    //    mapperBuilder.Build(propertyExpressionBuilder);

    //    //    return this;
    //}

    public ITypeMapperExpressionBuilder<TSource, TTarget> ApplyNameMatch()
    {
        var sourceGetProperties = typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.GetMethod?.IsPublic == true);
        var targetSetProperties = typeof(TTarget).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.SetMethod?.IsPublic == true).ToDictionary(x => x.Name);

        foreach (var sourceGetProperty in sourceGetProperties)
        {
            if (targetSetProperties.TryGetValue(sourceGetProperty.Name, out var targetSetProperty))
            {
                var propertyExpression = BuildPropertyAssignment(parameterSource, sourceGetProperty, parameterTarget, targetSetProperty, _serviceProvider);
                if (propertyExpression != null)
                {
                    _expressions.Add(propertyExpression);
                }
            }
        }

        return this;
    }

    public Expression<Action<TSource, TTarget>> Build()
    {
        var allPropertyExpressions = Expression.Block(_expressions);
        return Expression.Lambda<Action<TSource, TTarget>>(allPropertyExpressions, parameterSource, parameterTarget);
    }

    private static Expression? BuildPropertyAssignment(ParameterExpression parameterSource, PropertyInfo sourceGetProperty, ParameterExpression parameterTarget, PropertyInfo targetSetProperty, IServiceProvider serviceProvider)
    {
        Expression? expression = null;
        if (targetSetProperty.PropertyType.IsAssignableFrom(sourceGetProperty.PropertyType))
        {
            expression = BuildAssignablePropertyAssignment(parameterSource, sourceGetProperty, parameterTarget, targetSetProperty);
        }
        else if (targetSetProperty.PropertyType.IsClass && sourceGetProperty.PropertyType.IsClass && sourceGetProperty.PropertyType != typeof(string))
        {
            expression = BuildClassPropertyAssignment(parameterSource, sourceGetProperty, parameterTarget, targetSetProperty, serviceProvider);
        }
        else if (targetSetProperty.PropertyType.IsConstructedGenericType && sourceGetProperty.PropertyType.IsConstructedGenericType)
        {
            expression = BuildGenericEnumerablePropertyAssignment(parameterSource, sourceGetProperty, parameterTarget, targetSetProperty, serviceProvider);
        }

        return expression;
    }

    private static BinaryExpression BuildAssignablePropertyAssignment(ParameterExpression parameterSource, PropertyInfo sourceGetProperty, ParameterExpression parameterTarget, PropertyInfo targetSetProperty)
    {
        Expression sourceProperty = Expression.Property(parameterSource, sourceGetProperty);
        var targetProperty = Expression.Property(parameterTarget, targetSetProperty);
        if (sourceGetProperty.PropertyType != targetSetProperty.PropertyType)
        {
            sourceProperty = Expression.ConvertChecked(sourceProperty, targetSetProperty.PropertyType);
        }
        var assignment = Expression.Assign(targetProperty, sourceProperty);

        return assignment;
    }

    private static Expression? BuildClassPropertyAssignment(ParameterExpression parameterSource, PropertyInfo sourceGetProperty, ParameterExpression parameterTarget, PropertyInfo targetSetProperty, IServiceProvider serviceProvider)
    {
        Expression? expression = null;
        var constructor = targetSetProperty.PropertyType.GetConstructor(BindingFlags.Public | BindingFlags.Instance, []);
        if (constructor != null)
        {
            var genericTypeMapperType = typeof(ITypeMapper<,>).MakeGenericType(sourceGetProperty.PropertyType, targetSetProperty.PropertyType);
            var genericTypeMapper = serviceProvider.GetService(genericTypeMapperType);
            if (genericTypeMapper != null)
            {
                var method = genericTypeMapperType.GetMethod(nameof(ITypeMapper<object, object>.Map), BindingFlags.Public | BindingFlags.Instance);

                Expression sourceProperty = Expression.Property(parameterSource, sourceGetProperty);
                Expression targetProperty = Expression.Property(parameterTarget, targetSetProperty);
                var nullExpression = Expression.Constant(null, sourceGetProperty.PropertyType);

                var newPropertyVariable = Expression.Variable(targetSetProperty.PropertyType, "newTargetValue");
                var newPropertyType = Expression.New(targetSetProperty.PropertyType);
                var newTargetValue = Expression.Assign(newPropertyVariable, newPropertyType);
                var mapper = Expression.Convert(Expression.Constant(genericTypeMapper), method!.DeclaringType!);
                var mapperMethod = Expression.Call(mapper, method, sourceProperty, newPropertyVariable);
                var assignmentMapped = Expression.Assign(targetProperty, newPropertyVariable);
                var blockAssignment = Expression.Block([newPropertyVariable], newTargetValue, mapperMethod, assignmentMapped);
                var assignment = Expression.IfThenElse(
                    Expression.Equal(sourceProperty, nullExpression),
                    Expression.Assign(targetProperty, Expression.Constant(null, targetSetProperty.PropertyType)),
                    blockAssignment);

                expression = assignment;
            }

        }

        return expression;
    }

    private static Expression? BuildGenericEnumerablePropertyAssignment(ParameterExpression parameterSource, PropertyInfo sourceGetProperty, ParameterExpression parameterTarget, PropertyInfo targetSetProperty, IServiceProvider serviceProvider)
    {
        Expression? expression = null;

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
                var genericTypeMapper = serviceProvider.GetService(genericTypeMapperType);

                if (genericTypeMapper != null)
                {
                    var method = genericTypeMapperType.GetMethod(nameof(ITypeMapper<object, object>.Map), BindingFlags.Public | BindingFlags.Instance);
                    var targetGenericListType = typeof(List<>).MakeGenericType(targetGenericType);

                    Expression sourceProperty = Expression.Property(parameterSource, sourceGetProperty);
                    Expression targetProperty = Expression.Property(parameterTarget, targetSetProperty);
                    var nullExpression = Expression.Constant(null, sourceGetProperty.PropertyType);
                    var mapper = Expression.Convert(Expression.Constant(genericTypeMapper), method!.DeclaringType!);

                    var newPropertyList = Expression.Variable(targetGenericListType, "newTargetList");
                    var newList = Expression.New(targetGenericListType);
                    var newPropertyListValue = Expression.Assign(newPropertyList, newList);
                    var listAddMethod = targetGenericListType.GetMethod(nameof(List<object>.Add), BindingFlags.Public | BindingFlags.Instance);

                    var parameterLoop = Expression.Parameter(sourceGenericType);

                    var newPropertyVariable = Expression.Variable(targetGenericType, "newTargetValue");
                    var newPropertyType = Expression.New(targetGenericType);
                    var newTargetValue = Expression.Assign(newPropertyVariable, newPropertyType);

                    var mapperMethod = Expression.Call(mapper, method, parameterLoop, newPropertyVariable);
                    var targetValueAdded = Expression.Call(newPropertyList, listAddMethod!, newPropertyVariable);
                    var blockAdd = Expression.Block([newPropertyVariable], newTargetValue, targetValueAdded, mapperMethod);

                    var foreachExpression = ForEach(sourceProperty, parameterLoop, blockAdd);
                    var setTargetMappedValue = Expression.Assign(targetProperty, newPropertyList);
                    var buildMappedList = Expression.Block([newPropertyList], newPropertyListValue, foreachExpression, setTargetMappedValue);
                    var assignment = Expression.IfThenElse(
                        Expression.Equal(sourceProperty, nullExpression),
                        Expression.Assign(targetProperty, Expression.Constant(null, targetSetProperty.PropertyType)),
                        buildMappedList);

                    expression = assignment;
                }
            }
        }

        return expression;
    }

    private static BlockExpression ForEach(Expression enumerable, ParameterExpression loopVar, Expression loopContent)
    {
        var enumerableType = enumerable.Type;
        var getEnumerator = enumerableType.GetMethod("GetEnumerator");
        getEnumerator ??= enumerableType.GetInterfaces().First(x => x.GetGenericTypeDefinition() == typeof(IEnumerable<>)).GetMethod("GetEnumerator");
        var enumeratorType = getEnumerator!.ReturnType;
        var enumerator = Expression.Variable(enumeratorType, "enumerator");

        return Expression.Block([enumerator],
            Expression.Assign(enumerator, Expression.Call(enumerable, getEnumerator)),
            EnumerationLoop(enumerator,
                Expression.Block([loopVar],
                    Expression.Assign(loopVar, Expression.Property(enumerator, "Current")),
                    loopContent)));
    }

    private static Expression EnumerationLoop(ParameterExpression enumerator, Expression loopContent)
    {
        var loop = While(
            Expression.Call(enumerator, typeof(IEnumerator).GetMethod("MoveNext")!),
            loopContent);

        var enumeratorType = enumerator.Type;
        if (typeof(IDisposable).IsAssignableFrom(enumeratorType))
            return Using(enumerator, loop);

        if (!enumeratorType.IsValueType)
        {
            var disposable = Expression.Variable(typeof(IDisposable), "disposable");
            return Expression.TryFinally(
                loop,
                Expression.Block([disposable],
                    Expression.Assign(disposable, Expression.TypeAs(enumerator, typeof(IDisposable))),
                    Expression.IfThen(
                        Expression.NotEqual(disposable, Expression.Constant(null)),
                        Expression.Call(disposable, typeof(IDisposable).GetMethod("Dispose")!))));
        }

        return loop;
    }

    private static TryExpression Using(ParameterExpression variable, Expression content)
    {
        var variableType = variable.Type;

        if (!typeof(IDisposable).IsAssignableFrom(variableType))
            throw new Exception($"'{variableType.FullName}': type used in a using statement must be implicitly convertible to 'System.IDisposable'");

        var disposeMethod = typeof(IDisposable).GetMethod("Dispose");

        if (variableType.IsValueType)
        {
            return Expression.TryFinally(
                content,
                Expression.Call(Expression.Convert(variable, typeof(IDisposable)), disposeMethod!));
        }

        if (variableType.IsInterface)
        {
            return Expression.TryFinally(
                content,
                Expression.IfThen(
                    Expression.NotEqual(variable, Expression.Constant(null)),
                    Expression.Call(variable, disposeMethod!)));
        }

        return Expression.TryFinally(
            content,
            Expression.IfThen(
                Expression.NotEqual(variable, Expression.Constant(null)),
                Expression.Call(Expression.Convert(variable, typeof(IDisposable)), disposeMethod!)));
    }

    private static LoopExpression While(Expression loopCondition, Expression loopContent)
    {
        var breakLabel = Expression.Label();
        return Expression.Loop(
            Expression.IfThenElse(
                loopCondition,
                loopContent,
                Expression.Break(breakLabel)),
            breakLabel);
    }
}
