namespace OrderHub.Core.Services;

/// <summary>
/// 低庫存頁的一列資料（service 回傳給 Web 層映射用的 DTO）。
/// </summary>
public record LowStockItem(string Sku, string Name, int StockQuantity, int SoldLast30Days);
