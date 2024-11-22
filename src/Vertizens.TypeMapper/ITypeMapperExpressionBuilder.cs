using System.Linq.Expressions;

namespace Vertizens.TypeMapper;

/// <summary>
/// Methods use to define a mapper action
/// </summary>
/// <typeparam name="TSource">Type of source object to map from</typeparam>
/// <typeparam name="TTarget">Type of target object to map to</typeparam>
public interface ITypeMapperExpressionBuilder<TSource, TTarget>
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TProperty">Property value of both a property selector and value selector</typeparam>
    /// <param name="propertySelector">left side of an assignment</param>
    /// <param name="valueSelector">right side of an assignment</param>
    ITypeMapperExpressionBuilder<TSource, TTarget> Map<TProperty>(Expression<Func<TTarget, TProperty>> propertySelector, Expression<Func<TSource, TProperty>> valueSelector);

    /// <summary>
    /// Apply name match default behavior
    /// </summary>
    ITypeMapperExpressionBuilder<TSource, TTarget> ApplyNameMatch();

    /// <summary>
    /// Builds an expression that can be compiled into an action for mapping
    /// </summary>
    Expression<Action<TSource, TTarget>> Build();
}
