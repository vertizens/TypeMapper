using System.Linq.Expressions;

namespace Vertizens.TypeMapper;
public interface ITypeProjector<TSource, TTarget> where TTarget : class, new()
{
    Expression<Func<TSource, TTarget>> GetProjection();
}
