﻿using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace Vertizens.TypeMapper;
internal class DefaultTypeProjectorExpressionBuilder<TSource, TTarget>(
    IServiceProvider _serviceProvider
    ) : ITypeProjectorExpressionBuilder<TSource, TTarget> where TTarget : class, new()
{
    private Expression<Func<TSource, TTarget>> _projection = x => new TTarget();

    public ITypeProjectorExpressionBuilder<TSource, TTarget> Union(Expression<Func<TSource, TTarget>> projection)
    {
        if (_projection.Body.NodeType == ExpressionType.New)
        {
            _projection = projection;
        }
        else if (_projection.Body.NodeType == ExpressionType.MemberInit && projection.Body.NodeType == ExpressionType.MemberInit)
        {
            var projection1 = _projection;
            var projection2 = projection;
            var bindings1 = ((MemberInitExpression)projection1.Body).Bindings;
            var bindings2 = ((MemberInitExpression)projection2.Body).Bindings;
            if (bindings1.All(x => x.BindingType == MemberBindingType.Assignment) && bindings2.All(x => x.BindingType == MemberBindingType.Assignment))
            {
                var projection2ReplacedBody = ReplaceParameterExpressionVisitor.ReplaceParameter(projection.Body, projection2.Parameters[0], projection1.Parameters[0]);
                var bindingsByMember1 = bindings1.ToDictionary(x => x.Member, x => (MemberAssignment)x);
                IList<MemberAssignment> newAssignments = [];
                foreach (var binding in ((MemberInitExpression)projection2ReplacedBody).Bindings)
                {
                    if (bindingsByMember1.TryGetValue(binding.Member, out var existingBinding))
                    {
                        bindingsByMember1[binding.Member] = (MemberAssignment)binding;
                    }
                    else
                    {
                        newAssignments.Add((MemberAssignment)binding);
                    }
                }

                var allMemberAssignments = bindingsByMember1.Values.Concat(newAssignments);
                var memberInit = Expression.MemberInit(Expression.New(typeof(TTarget)), allMemberAssignments);

                _projection = Expression.Lambda<Func<TSource, TTarget>>(memberInit, _projection.Parameters);
            }
            else
            {
                throw new ArgumentException("Both body expressions must use Assignment member bindings only");
            }
        }
        else
        {
            throw new ArgumentException("Both body expressions need to be MemberInit nodes");
        }

        return this;
    }

    public ITypeProjectorExpressionBuilder<TSource, TTarget> ApplyNameMatch()
    {
        var sourceGetProperties = typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.GetMethod?.IsPublic == true);
        var targetSetProperties = typeof(TTarget).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.SetMethod?.IsPublic == true).ToDictionary(x => x.Name);
        var parameterSource = Expression.Parameter(typeof(TSource), "source");

        IList<MemberBinding> memberBindings = [];
        foreach (var sourceGetProperty in sourceGetProperties)
        {
            if (targetSetProperties.TryGetValue(sourceGetProperty.Name, out var targetSetProperty))
            {
                var memberBinding = BuildPropertyBinding(parameterSource, sourceGetProperty, targetSetProperty, _serviceProvider);
                if (memberBinding != null)
                {
                    memberBindings.Add(memberBinding);
                }
            }
        }

        var newTarget = Expression.New(typeof(TTarget));
        var body = Expression.MemberInit(newTarget, memberBindings);

        Union(Expression.Lambda<Func<TSource, TTarget>>(body, parameterSource));

        return this;
    }

    public Expression<Func<TSource, TTarget>> Build()
    {
        return _projection;
    }

    private static MemberBinding? BuildPropertyBinding(ParameterExpression parameterSource, PropertyInfo sourceGetProperty, PropertyInfo targetSetProperty, IServiceProvider serviceProvider)
    {
        MemberBinding? memberBinding = null;
        if (targetSetProperty.PropertyType.IsAssignableFrom(sourceGetProperty.PropertyType))
        {
            memberBinding = BuildAssignablePropertyBinding(parameterSource, sourceGetProperty, targetSetProperty);
        }
        else if (targetSetProperty.PropertyType.IsClass && sourceGetProperty.PropertyType.IsClass && sourceGetProperty.PropertyType != typeof(string))
        {
            memberBinding = BuildClassPropertyBinding(parameterSource, sourceGetProperty, targetSetProperty, serviceProvider);
        }
        else if (targetSetProperty.PropertyType.IsConstructedGenericType && sourceGetProperty.PropertyType.IsConstructedGenericType)
        {
            memberBinding = BuildGenericEnumerablePropertyBinding(parameterSource, sourceGetProperty, targetSetProperty, serviceProvider);
        }

        return memberBinding;
    }

    private static MemberAssignment BuildAssignablePropertyBinding(ParameterExpression parameterSource, PropertyInfo sourceGetProperty, PropertyInfo targetSetProperty)
    {
        Expression sourceProperty = Expression.Property(parameterSource, sourceGetProperty);
        if (sourceGetProperty.PropertyType != targetSetProperty.PropertyType)
        {
            sourceProperty = Expression.ConvertChecked(sourceProperty, targetSetProperty.PropertyType);
        }
        var propertyBinding = Expression.Bind(targetSetProperty, sourceProperty);

        return propertyBinding;
    }

    private static MemberBinding? BuildClassPropertyBinding(ParameterExpression parameterSource, PropertyInfo sourceGetProperty, PropertyInfo targetSetProperty, IServiceProvider serviceProvider)
    {
        MemberBinding? memberBinding = null;
        var constructor = targetSetProperty.PropertyType.GetConstructor(BindingFlags.Public | BindingFlags.Instance, []);
        if (constructor != null)
        {
            var genericTypeProjectorType = typeof(ITypeProjector<,>).MakeGenericType(sourceGetProperty.PropertyType, targetSetProperty.PropertyType);
            var genericTypeProjector = serviceProvider.GetService(genericTypeProjectorType);
            if (genericTypeProjector != null)
            {
                Expression sourceProperty = Expression.Property(parameterSource, sourceGetProperty);
                var method = genericTypeProjectorType.GetMethod(nameof(ITypeProjector<object, object>.GetProjection), BindingFlags.Public | BindingFlags.Instance);
                var nullExpression = Expression.Constant(null, sourceGetProperty.PropertyType);
                var expression = (LambdaExpression)method!.Invoke(genericTypeProjector, [])!;
                var projectedExpression = ReplaceParameterExpressionVisitor.ReplaceParameter(expression.Body, expression.Parameters[0], sourceProperty);
                var conditionExpression = Expression.Condition(Expression.Equal(sourceProperty, nullExpression), Expression.Constant(null, targetSetProperty.PropertyType), projectedExpression);
                memberBinding = Expression.Bind(targetSetProperty, conditionExpression);
            }
        }

        return memberBinding;
    }

    private static MemberBinding? BuildGenericEnumerablePropertyBinding(ParameterExpression parameterSource, PropertyInfo sourceGetProperty, PropertyInfo targetSetProperty, IServiceProvider serviceProvider)
    {
        MemberBinding? memberBinding = null;
        var targetGenericArguments = targetSetProperty.PropertyType.GetGenericArguments();
        var listType = typeof(List<>).MakeGenericType(targetGenericArguments);

        if (targetSetProperty.PropertyType.IsAssignableFrom(listType) && sourceGetProperty.PropertyType.IsAssignableTo(typeof(IEnumerable)))
        {
            var targetGenericType = targetGenericArguments.First();
            var sourceGenericType = sourceGetProperty.PropertyType.GetGenericArguments().FirstOrDefault();

            var constructor = targetGenericType.GetConstructor(BindingFlags.Public | BindingFlags.Instance, []);
            if (sourceGenericType != null && constructor != null)
            {
                var genericTypeProjectorType = typeof(ITypeProjector<,>).MakeGenericType(sourceGenericType, targetGenericType);
                var genericTypeProjector = serviceProvider.GetService(genericTypeProjectorType);

                if (genericTypeProjector != null)
                {
                    var method = genericTypeProjectorType.GetMethod(nameof(ITypeProjector<object, object>.GetProjection), BindingFlags.Public | BindingFlags.Instance);
                    var targetGenericListType = typeof(List<>).MakeGenericType(targetGenericType);

                    Expression sourceProperty = Expression.Property(parameterSource, sourceGetProperty);
                    var nullExpression = Expression.Constant(null, sourceGetProperty.PropertyType);
                    var expression = (LambdaExpression)method!.Invoke(genericTypeProjector, [])!;

                    var enumerableSelects = typeof(Enumerable).GetMember(nameof(Enumerable.Select), MemberTypes.Method, BindingFlags.Public | BindingFlags.Static);
                    var enumerableSelect = (MethodInfo)enumerableSelects.First(x => ((MethodInfo)x).GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>));
                    enumerableSelect = enumerableSelect.MakeGenericMethod(sourceGenericType, targetGenericType);
                    var enumerableToList = typeof(Enumerable).GetMethod(nameof(Enumerable.ToList), BindingFlags.Public | BindingFlags.Static);
                    enumerableToList = enumerableToList!.MakeGenericMethod(targetGenericType);
                    var projectedExpression = Expression.Call(null, enumerableSelect, sourceProperty, expression);
                    var projectedExpressionToList = Expression.Call(null, enumerableToList, projectedExpression);

                    var conditionExpression = Expression.Condition(Expression.Equal(sourceProperty, nullExpression), Expression.Constant(null, targetGenericListType), projectedExpressionToList);
                    memberBinding = Expression.Bind(targetSetProperty, conditionExpression);
                }
            }
        }
        return memberBinding;
    }
}
