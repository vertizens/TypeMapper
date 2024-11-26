namespace Vertizens.TypeMapper;
internal class DefaultTypeMapper<TSource, TTarget>(
    ITypeMapperExpressionBuilder<TSource, TTarget> _typeMapperExpressionBuilder,
    ITypeMapperBuilder<TSource, TTarget> _typeMapperBuilder
    ) : ITypeMapper<TSource, TTarget>
{
    private readonly Action<TSource, TTarget> _action = BuildAction(_typeMapperExpressionBuilder, _typeMapperBuilder);

    public void Map(TSource sourceObject, TTarget targetObject)
    {
        if (sourceObject == null)
        {
            throw new ArgumentNullException(nameof(sourceObject));
        }

        if (targetObject == null)
        {
            throw new ArgumentNullException(nameof(targetObject));
        }

        _action(sourceObject, targetObject);
    }

    private static Action<TSource, TTarget> BuildAction(
        ITypeMapperExpressionBuilder<TSource, TTarget> expressionBuilder,
        ITypeMapperBuilder<TSource, TTarget> typeMapperBuilder)
    {
        typeMapperBuilder.Build(expressionBuilder);
        return expressionBuilder.Build().Compile();
    }
}
