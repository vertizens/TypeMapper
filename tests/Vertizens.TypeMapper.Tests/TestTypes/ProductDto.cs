#nullable disable

namespace Vertizens.TypeMapper.Tests.TestTypes;
internal class ProductDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string Category { get; set; }
}
