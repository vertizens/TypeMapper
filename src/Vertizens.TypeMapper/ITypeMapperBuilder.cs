namespace Vertizens.TypeMapper;

/// <summary>
/// Adds to an expression builder that defines a <see cref="ITypeMapper{TSource, TTarget}"/>
/// </summary>
/// <typeparam name="TSource">Type of source object to map from</typeparam>
/// <typeparam name="TTarget">Type of target object to map to</typeparam>
public interface ITypeMapperBuilder<TSource, TTarget>
{
    /// <summary>
    /// Called with an expression builder that will in turn be used to define a mapping action in a <see cref="ITypeMapper{TSource, TTarget}"/>
    /// </summary>
    /// <param name="expressionBuilder">Expression Builder to define mapped properties</param>
    void Build(ITypeMapperExpressionBuilder<TSource, TTarget> expressionBuilder);
}
