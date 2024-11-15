namespace Vertizens.TypeMapper;
public interface ITypeMapper
{
    /// <summary>
    /// Instantiates a new <typeparamref name="TTarget"/> object and maps source properties on to it.
    /// </summary>
    /// <typeparam name="TSource">Type of source object to map from</typeparam>
    /// <typeparam name="TTarget">Type of target object to map to</typeparam>
    /// <param name="source"source object></param>
    /// <returns></returns>
    TTarget Map<TSource, TTarget>(TSource source) where TTarget : class, new();
    /// <summary>
    /// Maps source property values onto target instance properties
    /// </summary>
    /// <typeparam name="TSource">Type of source object to map from</typeparam>
    /// <typeparam name="TTarget">Type of target object to map to</typeparam>
    /// <param name="source">source object</param>
    /// <param name="target">target object</param>
    void Map<TSource, TTarget>(TSource source, TTarget target);
}

/// <summary>
/// Maps source property values onto target instance properties
/// </summary>
/// <typeparam name="TSource">Type of source object to map from</typeparam>
/// <typeparam name="TTarget">Type of target object to map to</typeparam>
public interface ITypeMapper<TSource, TTarget>
{
    /// <summary>
    /// Maps source property values onto target instance properties
    /// </summary>
    /// <param name="source">source object</param>
    /// <param name="target">target object</param>
    void Map(TSource source, TTarget target);
}
