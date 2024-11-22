using System.Linq.Expressions;

namespace Vertizens.TypeMapper;
internal class DefaultTypeProjector<TSource, TTarget>(
    ITypeProjectorExpressionBuilder<TSource, TTarget> _typeProjectorExpressionBuilder
    ) : ITypeProjector<TSource, TTarget> where TTarget : class, new()
{
    private readonly Expression<Func<TSource, TTarget>> _expression = _typeProjectorExpressionBuilder.ApplyNameMatch().Build();

    public Expression<Func<TSource, TTarget>> GetProjection()
    {
        return _expression;
    }
}
