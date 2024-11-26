using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using System.Reflection;

namespace Vertizens.TypeMapper;
internal class DefaultTypeMapperBuilder<TSource, TTarget>(
    IServiceProvider _serviceProvider
    ) : ITypeMapperBuilder<TSource, TTarget>
{
    public void Build(ITypeMapperExpressionBuilder<TSource, TTarget> expressionBuilder)
    {
        var constructor = typeof(TTarget).GetConstructor(BindingFlags.Public | BindingFlags.Instance, []);
        if (constructor != null)
        {
            var projectorExpressionsMethod = typeof(DefaultTypeMapperBuilder<TSource, TTarget>).GetMethod(nameof(GetProjectorExpressions), BindingFlags.NonPublic | BindingFlags.Static);
            projectorExpressionsMethod = projectorExpressionsMethod!.MakeGenericMethod(typeof(TSource), typeof(TTarget));

            var parameterSource = Expression.Parameter(typeof(TSource), "source");
            var parameterTarget = Expression.Parameter(typeof(TTarget), "target");
            projectorExpressionsMethod!.Invoke(null, [parameterSource, parameterTarget, _serviceProvider, expressionBuilder]);
        }
        else
        {
            expressionBuilder.ApplyNameMatch();
        }
    }

    private static void GetProjectorExpressions<TProjectorSource, TProjectorTarget>(
        ParameterExpression parameterSource,
        ParameterExpression parameterTarget,
        IServiceProvider serviceProvider,
        ITypeMapperExpressionBuilder<TSource, TTarget> expressionBuilder)
        where TProjectorTarget : class, new()
    {
        IList<Expression>? expressions = null;

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
                    if (binding.BindingType == MemberBindingType.Assignment)
                    {
                        var targetProperty = Expression.MakeMemberAccess(parameterTarget, binding.Member);
                        var memberExpression = ((MemberAssignment)binding).Expression;
                        memberExpression = ReplaceParameterExpressionVisitor.ReplaceParameter(memberExpression, projection.Parameters[0], parameterSource);
                        var expressionBuilderMapMethod = typeof(DefaultTypeMapperBuilder<TProjectorSource, TProjectorTarget>).GetMethod(nameof(ExpressionBuilderMap), BindingFlags.NonPublic | BindingFlags.Static);
                        var resultType = ((PropertyInfo)binding.Member).PropertyType;
                        expressionBuilderMapMethod = expressionBuilderMapMethod!.MakeGenericMethod(typeof(TProjectorSource), typeof(TProjectorTarget), resultType);

                        expressionBuilderMapMethod!.Invoke(null, [parameterSource, parameterTarget, targetProperty, memberExpression, expressionBuilder]);
                    }
                }
            }
        }
    }

    private static void ExpressionBuilderMap<TProjectorSource, TProjectorTarget, TResult>(
        ParameterExpression parameterSource,
        ParameterExpression parameterTarget,
        MemberExpression targetExpression,
        Expression valueExpression,
        ITypeMapperExpressionBuilder<TProjectorSource, TProjectorTarget> expressionBuilder)
    {
        var propertySelector = Expression.Lambda<Func<TProjectorTarget, TResult>>(targetExpression, parameterTarget);
        var valueSelector = Expression.Lambda<Func<TProjectorSource, TResult>>(valueExpression, parameterSource);
        expressionBuilder.Map(propertySelector, valueSelector);
    }
}
