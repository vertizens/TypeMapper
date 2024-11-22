using System.Linq.Expressions;

namespace Vertizens.TypeMapper;

/// <summary>
/// A type projection expression builder that builds an expression used to project one type to another
/// </summary>
/// <typeparam name="TSource">Type of source object to project from</typeparam>
/// <typeparam name="TTarget">Type of target object to project to</typeparam>
public interface ITypeProjectorExpressionBuilder<TSource, TTarget> where TTarget : class, new()
{
    /// <summary>
    /// Union existing projection with a custom projection, overwriting properties that already exist
    /// </summary>
    /// <param name="projection">projection to union the and overwrite the behavior with existing projection</param>
    ITypeProjectorExpressionBuilder<TSource, TTarget> Union(Expression<Func<TSource, TTarget>> projection);

    /// <summary>
    /// Applies default name matching behavior
    /// </summary>
    ITypeProjectorExpressionBuilder<TSource, TTarget> ApplyNameMatch();

    /// <summary>
    /// Returns the expression of the current projection
    /// </summary>
    Expression<Func<TSource, TTarget>> Build();
}
