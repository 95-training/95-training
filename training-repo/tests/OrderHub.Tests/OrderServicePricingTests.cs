using OrderHub.Core.Domain;
using OrderHub.Core.Services;

namespace OrderHub.Tests;

public class OrderServicePricingTests
{
    [Fact]
    public async Task CreateOrder_GoldCustomer_SnapshotsRawPrice_AndDiscountsTotalOnce()
    {
        // 回歸測試（客訴 2）：Gold 曾在建單時對單價先折一次，CalculateTotal 又對總額折一次
        // → 折兩次（1420 → 1150.20），比正確的 1278 少一截。Silver/Standard 不受影響。
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var gold = TestSetup.AddCustomer(db, tier: CustomerTier.Gold);
        var product = TestSetup.AddProduct(db, unitPrice: 1420m);

        var result = await service.CreateOrderAsync(gold.Id, new[] { new NewOrderLine(product.Id, 1) });
        Assert.True(result.Success);

        // 單價快照必須是原價，不能在建單時就先折
        Assert.Equal(1420m, result.Value!.Items.Single().UnitPriceSnapshot);

        // 應付總額只折一次：1420 × 0.90 = 1278（不是折兩次的 1150.20）
        var order = await service.GetOrderAsync(result.Value.Id);
        Assert.Equal(1278m, service.CalculateTotal(order!));
    }

    [Theory]
    [InlineData(CustomerTier.Standard, 0)]
    [InlineData(CustomerTier.Silver, 0.05)]
    [InlineData(CustomerTier.Gold, 0.10)]
    public void GetDiscountRate_ReturnsExpectedRate(CustomerTier tier, decimal expected)
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);

        Assert.Equal(expected, service.GetDiscountRate(tier));
    }

    [Fact]
    public void CalculateSubtotal_SumsQuantityTimesSnapshotPrice()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);

        var order = new Order
        {
            Items =
            {
                new OrderItem { Quantity = 2, UnitPriceSnapshot = 150m },
                new OrderItem { Quantity = 3, UnitPriceSnapshot = 40m }
            }
        };

        Assert.Equal(420m, service.CalculateSubtotal(order));
    }

    [Theory]
    [InlineData(CustomerTier.Standard, 1000, 1000)]
    [InlineData(CustomerTier.Silver, 1000, 950)]
    [InlineData(CustomerTier.Gold, 1000, 900)]
    public void CalculateTotal_AppliesTierDiscountOnSubtotal(CustomerTier tier, decimal unitPrice, decimal expectedTotal)
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);

        var order = new Order
        {
            Customer = new Customer { Tier = tier },
            Items = { new OrderItem { Quantity = 1, UnitPriceSnapshot = unitPrice } }
        };

        Assert.Equal(expectedTotal, service.CalculateTotal(order));
    }

    [Fact]
    public void CalculateTotal_WithoutCustomer_UsesStandardRate()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);

        var order = new Order
        {
            Items = { new OrderItem { Quantity = 2, UnitPriceSnapshot = 250m } }
        };

        Assert.Equal(500m, service.CalculateTotal(order));
    }
}
