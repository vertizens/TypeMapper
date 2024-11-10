namespace Vertizens.TypeMapper;
public interface INameMatchTypeProjector<TSource, TTarget> : ITypeProjector<TSource, TTarget> where TTarget : class, new()
{
}
