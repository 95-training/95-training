using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class OrderServiceQueryTests
{
    [Fact]
    public async Task GetOrders_WithStatusFilter_ReturnsOnlyMatchingStatus()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        db.Orders.AddRange(
            new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var result = await service.GetOrdersAsync(1, 20, OrderStatus.Shipped);

        Assert.All(result.Items, o => Assert.Equal(OrderStatus.Shipped, o.Status));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetOrders_ReportsTotalCountAndTotalPages()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        for (var i = 0; i < 45; i++)
            db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Confirmed, CreatedAt = DateTime.UtcNow.AddMinutes(-i) });
        db.SaveChanges();

        var result = await service.GetOrdersAsync(1, 20, null);

        Assert.Equal(45, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GetOrders_Page1_StartsAtNewestOrder_AndLastPageIsNotEmpty()
    {
        // 回歸測試（客訴 1）：1-based 的 page 曾用 Skip(page*pageSize)，
        // 導致第一頁跳過最新的 20 筆、最後一頁全空。
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        // 45 筆，i=0 最新（CreatedAt 最大）。pageSize 20 → 3 頁。
        for (var i = 0; i < 45; i++)
            db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Confirmed, CreatedAt = DateTime.UtcNow.AddMinutes(-i) });
        db.SaveChanges();
        var newestId = db.Orders.OrderByDescending(o => o.CreatedAt).First().Id;

        var page1 = await service.GetOrdersAsync(1, 20, null);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(newestId, page1.Items[0].Id);              // 最新訂單必須在第一頁第一列
        Assert.Contains(page1.Items, o => o.Id == newestId);

        var lastPage = await service.GetOrdersAsync(page1.TotalPages, 20, null);
        Assert.NotEmpty(lastPage.Items);                        // 最後一頁不可為空
        Assert.Equal(5, lastPage.Items.Count);                  // 45 = 20 + 20 + 5
    }

    [Fact]
    public async Task GetCustomerOrders_ReturnsOnlyThatCustomersOrders()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customerA = TestSetup.AddCustomer(db, name: "客戶A");
        var customerB = TestSetup.AddCustomer(db, name: "客戶B");

        db.Orders.AddRange(
            new Order { CustomerId = customerA.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customerB.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customerA.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var orders = await service.GetCustomerOrdersAsync(customerA.Id);

        Assert.Equal(2, orders.Count);
        Assert.All(orders, o => Assert.Equal(customerA.Id, o.CustomerId));
    }
}
