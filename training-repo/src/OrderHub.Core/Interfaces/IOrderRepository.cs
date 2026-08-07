using OrderHub.Core.Ai;
using OrderHub.Core.Common;
using OrderHub.Core.Domain;

namespace OrderHub.Core.Interfaces;

public interface IOrderRepository
{
    Task<PagedResult<Order>> GetPagedAsync(int page, int pageSize, OrderStatus? status);
    Task<Order?> GetWithDetailsAsync(int id);
    Task<IReadOnlyList<Order>> GetByCustomerAsync(int customerId);
    /// <summary>各商品自 since 起的售出總量（排除 Cancelled 訂單），key=ProductId。</summary>
    Task<IReadOnlyDictionary<int, int>> GetSoldQuantitiesSinceAsync(DateTime since);
    Task AddAsync(Order order);
    Task SaveChangesAsync();
    Task<IReadOnlyList<Order>> SearchAsync(OrderSearchQuery query);
}
