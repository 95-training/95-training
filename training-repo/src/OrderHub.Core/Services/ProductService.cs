using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;

namespace OrderHub.Core.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IOrderRepository _orderRepository;

    public ProductService(IProductRepository productRepository, IOrderRepository orderRepository)
    {
        _productRepository = productRepository;
        _orderRepository = orderRepository;
    }

    public Task<IReadOnlyList<Product>> GetAllAsync() => _productRepository.GetAllAsync();

    public Task<IReadOnlyList<Product>> GetActiveAsync() => _productRepository.GetActiveAsync();

    public async Task<IReadOnlyList<LowStockItem>> GetLowStockAsync(int threshold)
    {
        // 「近 30 天」是商業規則，在此決定視窗；資料抓取交給 repository。
        var since = DateTime.UtcNow.AddDays(-30);
        var products = await _productRepository.GetLowStockAsync(threshold);
        var sold = await _orderRepository.GetSoldQuantitiesSinceAsync(since);

        return products
            .Select(p => new LowStockItem(
                p.Sku, p.Name, p.StockQuantity,
                sold.TryGetValue(p.Id, out var q) ? q : 0))
            .ToList();
    }
}
