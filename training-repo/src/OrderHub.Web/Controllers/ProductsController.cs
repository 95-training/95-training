using Microsoft.AspNetCore.Mvc;
using OrderHub.Core.Services;
using OrderHub.Web.ViewModels;

namespace OrderHub.Web.Controllers;

public class ProductsController : Controller
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _productService.GetAllAsync();

        var vm = new ProductListViewModel
        {
            Products = products.Select(p => new ProductRowViewModel
            {
                Sku = p.Sku,
                Name = p.Name,
                UnitPrice = p.UnitPrice,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive
            }).ToList()
        };

        return View(vm);
    }

    public async Task<IActionResult> LowStock(LowStockViewModel query)
    {
        if (!ModelState.IsValid)
            return View(query);   // threshold <= 0 或非數字 → 顯示表單錯誤，非 500

        var items = await _productService.GetLowStockAsync(query.Threshold);

        query.Products = items.Select(i => new LowStockRowViewModel
        {
            Sku = i.Sku,
            Name = i.Name,
            StockQuantity = i.StockQuantity,
            SoldLast30Days = i.SoldLast30Days
        }).ToList();

        return View(query);
    }
}

