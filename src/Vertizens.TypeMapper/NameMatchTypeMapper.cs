using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace Vertizens.TypeMapper;
internal class NameMatchTypeMapper<TSource, TTarget>(
    IServiceProvider serviceProvider
    ) : INameMatchTypeMapper<TSource, TTarget>
{
    private readonly Action<TSource, TTarget> _action = BuildAction(serviceProvider);

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

        _action(sourceObject, targetObject);
    }

    private static Action<TSource, TTarget> BuildAction(IServiceProvider serviceProvider)
    {
        var projectorExpressionsMethod = typeof(NameMatchTypeMapper<TSource, TTarget>).GetMethod(nameof(GetProjectorExpressions), BindingFlags.NonPublic | BindingFlags.Static);
        projectorExpressionsMethod = projectorExpressionsMethod!.MakeGenericMethod(typeof(TSource), typeof(TTarget));

        var parameterSource = Expression.Parameter(typeof(TSource), "source");
        var parameterTarget = Expression.Parameter(typeof(TTarget), "target");
        var expressions = (IList<Expression>?)projectorExpressionsMethod!.Invoke(null, [parameterSource, parameterTarget, serviceProvider]);

        if (expressions == null)
        {
            var sourceGetProperties = typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.GetMethod?.IsPublic == true);
            var targetSetProperties = typeof(TTarget).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.SetMethod?.IsPublic == true).ToDictionary(x => x.Name);

            expressions = [];
            foreach (var sourceGetProperty in sourceGetProperties)
            {
                if (targetSetProperties.TryGetValue(sourceGetProperty.Name, out var targetSetProperty))
                {
                    var propertyExpression = BuildPropertyAssignment(parameterSource, sourceGetProperty, parameterTarget, targetSetProperty, serviceProvider);
                    if (propertyExpression != null)
                    {
                        expressions.Add(propertyExpression);
                    }
                }
            }
        }

        var allPropertyExpressions = Expression.Block(expressions);
        var expression = Expression.Lambda<Action<TSource, TTarget>>(allPropertyExpressions, parameterSource, parameterTarget);
        return expression.Compile();
    }

    private static IList<Expression>? GetProjectorExpressions<TProjectorSource, TProjectorTarget>(ParameterExpression parameterSource, ParameterExpression parameterTarget, IServiceProvider serviceProvider)
        where TProjectorTarget : class, new()
    {
        IList<Expression>? expressions = null;
        var constructor = typeof(TTarget).GetConstructor(BindingFlags.Public | BindingFlags.Instance, []);
        if (constructor != null)
        {
            var typeProjector = serviceProvider.GetService<ITypeProjector<TProjectorSource, TProjectorTarget>>();
            if (typeProjector != null)
            {
                var projection = typeProjector.GetProjection();
                if (projection.Body.NodeType == ExpressionType.MemberInit)
                {
                    expressions = [];
                    var bindings = ((MemberInitExpression)projection.Body).Bindings;
                    foreach (var binding in bindings)
                    {
                        var targetProperty = Expression.MakeMemberAccess(parameterTarget, binding.Member);
                        if (binding.BindingType == MemberBindingType.Assignment)
                        {
                            var memberExpression = ((MemberAssignment)binding).Expression;
                            memberExpression = ReplaceParameterExpressionVisitor.ReplaceParameter(memberExpression, projection.Parameters[0], parameterSource);
                            var assignment = Expression.Assign(targetProperty, memberExpression);
                            expressions.Add(assignment);
                        }
                    }
                }
            }
        }

        return expressions;
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

    private static Expression BuildAssignablePropertyAssignment(ParameterExpression parameterSource, PropertyInfo sourceGetProperty, ParameterExpression parameterTarget, PropertyInfo targetSetProperty)
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

    private static Expression ForEach(Expression enumerable, ParameterExpression loopVar, Expression loopContent)
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

    private static Expression Using(ParameterExpression variable, Expression content)
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

    private static Expression While(Expression loopCondition, Expression loopContent)
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
