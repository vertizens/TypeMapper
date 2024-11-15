using System.Linq.Expressions;

namespace Vertizens.TypeMapper;
/// <summary>
/// Projects using an expression from one source object to a target object.
/// </summary>
/// <typeparam name="TSource">Type of source object to project from</typeparam>
/// <typeparam name="TTarget">Type of target object to project to</typeparam>
public interface ITypeProjector<TSource, TTarget> where TTarget : class, new()
{
    /// <summary>
    /// Gets a projection for the defined <typeparamref name="TSource"/> to <typeparamref name="TTarget"/>
    /// </summary>
    /// <returns>Expression to be used for projection in Queryable</returns>
    Expression<Func<TSource, TTarget>> GetProjection();
}
