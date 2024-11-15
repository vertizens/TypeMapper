namespace Vertizens.TypeMapper;
/// <summary>
/// Projects using an expression from one source object to a target object.
/// Performs default Name/Type property match only 
/// </summary>
/// <typeparam name="TSource">Type of source object to project from</typeparam>
/// <typeparam name="TTarget">Type of target object to project to</typeparam>
public interface INameMatchTypeProjector<TSource, TTarget> : ITypeProjector<TSource, TTarget> where TTarget : class, new()
{
}
