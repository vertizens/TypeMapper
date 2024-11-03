using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Vertizens.TypeMapper.Tests.TestTypes;
using Xunit.Abstractions;

namespace Vertizens.TypeMapper.Tests;

public class NameMatchTypeMapperTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITestOutputHelper _output;

    public NameMatchTypeMapperTests(ITestOutputHelper output)
    {
        var services = new ServiceCollection().AddTypeMappers();
        _serviceProvider = services.BuildServiceProvider();
        _output = output;
    }
    [Fact]
    public void TestStringPropertyValue()
    {
        var mapper = _serviceProvider.GetRequiredService<INameMatchTypeMapper<StringProperty1, StringProperty2>>();

        var source = new StringProperty1 { Name = "Test1" };
        var target = new StringProperty2();

        mapper.Map(source, target);

        Assert.True(source.Name == target.Name);
    }

    [Fact]
    public void TestMismatchedStringNullabilityPropertyValue()
    {
        var mapper = _serviceProvider.GetRequiredService<INameMatchTypeMapper<StringNullableProperty1, StringProperty2>>();

        var source = new StringNullableProperty1 { Name = "Test1" };
        var target = new StringProperty2();

        mapper.Map(source, target);

        Assert.True(source.Name == target.Name);
    }

    [Fact]
    public void TestMismatchedStringNullabilityPropertyNull()
    {
        var mapper = _serviceProvider.GetRequiredService<INameMatchTypeMapper<StringNullableProperty1, StringProperty2>>();

        var source = new StringNullableProperty1 { Name = null };
        var target = new StringProperty2();

        mapper.Map(source, target);

        Assert.True(target.Name == null);
    }

    [Fact]
    public void TestMismatchedStringClassProperty()
    {
        var mapper = _serviceProvider.GetRequiredService<INameMatchTypeMapper<StringProperty1, ClassNameProperty1>>();

        var source = new StringProperty1 { Name = "Test1" };
        var target = new ClassNameProperty1();

        mapper.Map(source, target);

        Assert.True(target.NestedParent == null);
    }

    [Fact]
    public void TestMultipleProperty()
    {
        var mapper = _serviceProvider.GetRequiredService<INameMatchTypeMapper<MultipleProperty1, MultipleProperty1>>();

        var source = new MultipleProperty1
        {
            FirstName = "First",
            LastName = "Last",
            SomeDateTime = DateTime.Now,
            SomeDecimal = 324.43m,
            SomeInt = 42
        };
        var target = new MultipleProperty1();

        mapper.Map(source, target);

        Assert.True(source.FirstName == target.FirstName);
        Assert.True(source.LastName == target.LastName);
        Assert.True(source.SomeDateTime == target.SomeDateTime);
        Assert.True(source.SomeDecimal == target.SomeDecimal);
        Assert.True(source.SomeInt == target.SomeInt);
    }

    [Fact]
    public void TestMultiplePropertyMismatchedNullability()
    {
        var mapper = _serviceProvider.GetRequiredService<INameMatchTypeMapper<MultipleProperty2, MultipleProperty1>>();

        var source = new MultipleProperty2
        {
            SomeDecimal = 324.43m
        };
        var target = new MultipleProperty1();

        mapper.Map(source, target);

        Assert.True(source.FirstName == target.FirstName);
        Assert.True(source.LastName == target.LastName);
        Assert.True(source.SomeDateTime != target.SomeDateTime);
        Assert.True(source.SomeDecimal != target.SomeDecimal);
        Assert.True(source.SomeInt != target.SomeInt);
    }

    [Fact]
    public void TestNestedChildPropertyAsClass()
    {
        var mapper = _serviceProvider.GetRequiredService<INameMatchTypeMapper<NestedParent1, NestedParent2>>();

        var source = new NestedParent1
        {
            ParentId = 3,
            Child1 = new NestedChild1 { ChildId = 4 }
        };
        var target = new NestedParent2();

        mapper.Map(source, target);

        Assert.True(source.ParentId == target.ParentId);
        Assert.True(target.Child1 != null);
        Assert.True(source.Child1.ChildId == target.Child1?.ChildId);
    }

    [Fact]
    public void TestStringEnumerableToListProperty()
    {
        var mapper = _serviceProvider.GetRequiredService<INameMatchTypeMapper<IEnumerableProperty1, IListProperty1>>();

        var source = new IEnumerableProperty1
        {
            List1 = ["test1", "test2"]
        };
        var target = new IListProperty1();

        mapper.Map(source, target);

        Assert.True(target.List1 == null);
    }

    [Fact]
    public void TestStringListToEnumerableProperty()
    {
        var mapper = _serviceProvider.GetRequiredService<INameMatchTypeMapper<IListProperty1, IEnumerableProperty1>>();

        var source = new IListProperty1
        {
            List1 = ["test1", "test2"]
        };
        var target = new IEnumerableProperty1();

        mapper.Map(source, target);

        Assert.True(target.List1 != null);
        Assert.Equal(source.List1, target.List1);
    }

    [Fact]
    public void TestStringArrayToListProperty()
    {
        var mapper = _serviceProvider.GetRequiredService<INameMatchTypeMapper<ArrayProperty1, IListProperty1>>();

        var source = new ArrayProperty1
        {
            List1 = ["test1", "test2"]
        };
        var target = new IListProperty1();

        mapper.Map(source, target);

        Assert.True(target.List1 != null);
        Assert.Equal(source.List1, target.List1);
    }

    [Fact]
    public void TestClassListToListProperty()
    {
        var mapper = _serviceProvider.GetRequiredService<INameMatchTypeMapper<IListClassProperty1, IListClassProperty2>>();

        var source = new IListClassProperty1
        {
            List1 = [new NestedParent1 { ParentId = 2, Child1 = new NestedChild1 { ChildId = 4 } }]
        };
        var target = new IListClassProperty2();

        mapper.Map(source, target);

        Assert.True(target.List1 != null);

        var sourceParent = source.List1.First();
        var targetParent = target.List1.First();
        Assert.True(sourceParent.ParentId == targetParent.ParentId);
        Assert.True(targetParent.Child1 != null);
        Assert.True(sourceParent.Child1.ChildId == targetParent.Child1?.ChildId);
    }

    [Fact]
    public void TestNestedListPerformance()
    {
        var mapper = _serviceProvider.GetRequiredService<INameMatchTypeMapper<IListClassProperty1, IListClassProperty2>>();

        var source = new IListClassProperty1
        {
            List1 = [new NestedParent1 { ParentId = 2, Child1 = new NestedChild1 { ChildId = 4 } }]
        };

        var target = new IListClassProperty2();
        mapper.Map(source, target);

        var iterationCount = 1000000;
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < iterationCount; i++)
        {
            target = new IListClassProperty2();
            mapper.Map(source, target);
        }

        stopwatch.Stop();
        _output.WriteLine($"{nameof(TestNestedListPerformance)}: {stopwatch.Elapsed.TotalMilliseconds} ns per");

        Assert.True(target.List1 != null);

        var sourceParent = source.List1.First();
        var targetParent = target.List1.First();
        Assert.True(sourceParent.ParentId == targetParent.ParentId);
        Assert.True(targetParent.Child1 != null);
        Assert.True(sourceParent.Child1.ChildId == targetParent.Child1?.ChildId);
    }

    [Fact]
    public void TestSimplePerformance()
    {
        var mapper = _serviceProvider.GetRequiredService<INameMatchTypeMapper<Product, ProductDto>>();

        var source = new Product
        {
            Category = "Category1",
            Price = 9.99m,
            ProductId = 234,
            ProductName = "Test Product Name",
            StockQuantity = 12
        };

        var target = new ProductDto();
        mapper.Map(source, target);

        var iterationCount = 1000000;
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < iterationCount; i++)
        {
            target = new ProductDto();
            mapper.Map(source, target);
        }

        stopwatch.Stop();
        _output.WriteLine($"{nameof(TestSimplePerformance)}: {stopwatch.Elapsed.TotalMilliseconds} ns per");

        Assert.Equal(source.Category, target.Category);
        Assert.Equal(source.Price, target.Price);
        Assert.Equal(source.ProductId, target.ProductId);
        Assert.Equal(source.ProductName, target.ProductName);
        Assert.Equal(source.StockQuantity, target.StockQuantity);
    }
}