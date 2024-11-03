namespace Vertizens.TypeMapper;
public interface ITypeMapper
{
    TTarget Map<TSource, TTarget>(TSource source) where TTarget : class, new();
    void Map<TSource, TTarget>(TSource source, TTarget target);
}

public interface ITypeMapper<TSource, TTarget>
{
    void Map(TSource source, TTarget target);
}
