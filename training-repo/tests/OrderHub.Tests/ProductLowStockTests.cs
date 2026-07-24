using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class ProductLowStockTests
{
    [Fact]
    public async Task GetLowStock_FiltersByThreshold_AndSortsByStockAscending()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, stock: 8, sku: "SKU-B008");
        TestSetup.AddProduct(db, stock: 3, sku: "SKU-A003");
        TestSetup.AddProduct(db, stock: 10, sku: "SKU-EQ010"); // 剛好等於門檻 → 是 `<` 不是 `<=`，應排除
        TestSetup.AddProduct(db, stock: 20, sku: "SKU-C020");   // 高於門檻，不應出現

        var result = await service.GetLowStockAsync(10);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 3, 8 }, result.Select(r => r.StockQuantity).ToArray()); // 升冪
        Assert.DoesNotContain(result, r => r.Sku == "SKU-EQ010"); // 邊界：等於門檻不算低庫存
        Assert.DoesNotContain(result, r => r.Sku == "SKU-C020");
    }

    [Fact]
    public async Task GetLowStock_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, stock: 4, sku: "SKU-ACTIVE");
        TestSetup.AddProduct(db, stock: 2, sku: "SKU-DEAD", isActive: false); // 停售，即使低庫存也排除

        var result = await service.GetLowStockAsync(10);

        Assert.Single(result);
        Assert.Equal("SKU-ACTIVE", result[0].Sku);
    }

    [Fact]
    public async Task GetLowStock_SoldLast30Days_ExcludesCancelledAndOlderThan30Days()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, stock: 3, sku: "SKU-SOLD");

        // 25 天前、已確認、qty 2 → 計入
        db.Orders.Add(new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Confirmed,
            CreatedAt = DateTime.UtcNow.AddDays(-25),
            Items = { new OrderItem { ProductId = product.Id, Quantity = 2, UnitPriceSnapshot = 100m } }
        });
        // 25 天前、已取消、qty 5 → 排除
        db.Orders.Add(new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Cancelled,
            CreatedAt = DateTime.UtcNow.AddDays(-25),
            Items = { new OrderItem { ProductId = product.Id, Quantity = 5, UnitPriceSnapshot = 100m } }
        });
        // 40 天前、已確認、qty 7 → 超出 30 天，排除
        db.Orders.Add(new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Confirmed,
            CreatedAt = DateTime.UtcNow.AddDays(-40),
            Items = { new OrderItem { ProductId = product.Id, Quantity = 7, UnitPriceSnapshot = 100m } }
        });
        db.SaveChanges();

        var result = await service.GetLowStockAsync(10);

        Assert.Single(result);
        Assert.Equal(2, result[0].SoldLast30Days); // 只算 25 天前那筆已確認的 qty 2
    }
}
