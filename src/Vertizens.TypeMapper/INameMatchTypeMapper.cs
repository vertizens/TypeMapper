namespace Vertizens.TypeMapper;
/// <summary>
/// Maps source object to a target object.
/// Performs default Name/Type property match only 
/// </summary>
/// <typeparam name="TSource">Type of source object to map from</typeparam>
/// <typeparam name="TTarget">Type of target object to map to</typeparam>
public interface INameMatchTypeMapper<TSource, TTarget> : ITypeMapper<TSource, TTarget>
{
}
