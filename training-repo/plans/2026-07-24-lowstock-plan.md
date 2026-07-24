# 練習 3 — 低庫存警示頁面 `GET /Products/LowStock`

## Context

採購同事需要一頁快速看到「快沒貨」的商品。目前 `/Products` 只列全部商品，沒有門檻篩選，也沒有「近期賣了多少」的資訊，無法判斷該補哪些貨。本功能新增一頁：輸入庫存門檻，列出仍在販售且庫存低於門檻的商品，並顯示近 30 天實際售出量（排除已取消訂單），依庫存由少到多排序，讓採購一眼看出優先補貨對象。

嚴格遵循既有 Products 慣例：Controller 薄、商業邏輯在 Core service、EF 查詢在 repository、View 綁 ViewModel、DataAnnotations + ModelState 驗證。只做這一頁，不順手重構其他東西（重構留練習 4）。

## 規格對照（驗收條件）
- 路由 `GET /Products/LowStock?threshold=10`；未帶 threshold 預設 10。
- 列出 `IsActive == true` 且 `StockQuantity < threshold` 的商品，依 `StockQuantity` **升冪**。
- 欄位：Sku、名稱、現有庫存、近 30 天售出數量（排除 `Cancelled` 訂單）。
- 庫存 `< 5` 的列標 `table-danger`。
- `threshold <= 0`（或非數字）→ 頁面顯示表單驗證錯誤，**不可 500**。
- 導覽列加「低庫存」連結。
- ≥ 3 個 service 層單元測試。

## 分層設計（資料流）

`ProductsController.LowStock` → `IProductService.GetLowStockAsync(threshold)` → 兩個 repository 查詢 → service 合併 → Core DTO → controller 映射成 ViewModel → View。

「近 30 天」「合併售出量」屬商業規則，放 service；「篩門檻/排序」「彙總 OrderItems」屬資料查詢，放 repository；`_db` 只在 repository 出現。**兩個查詢、無 N+1。**

### 檔案清單

1. `src/OrderHub.Core/Services/LowStockItem.cs`（新增）— Core DTO `record LowStockItem(string Sku, string Name, int StockQuantity, int SoldLast30Days)`。
2. `IProductRepository` + `ProductRepository`（修改）— `GetLowStockAsync(int threshold)`：`Where(p => p.IsActive && p.StockQuantity < threshold).OrderBy(StockQuantity).ThenBy(Sku)`。
3. `IOrderRepository` + `OrderRepository`（修改）— `GetSoldQuantitiesSinceAsync(DateTime since)`：`_db.OrderItems` where `Order.CreatedAt >= since && Status != Cancelled`，GROUP BY ProductId，回傳 `IReadOnlyDictionary<int,int>`。
4. `IProductService` + `ProductService`（修改）— 新增注入 `IOrderRepository`；`GetLowStockAsync(threshold)` 用 `since = UtcNow.AddDays(-30)`，合併兩查詢成 `LowStockItem` 清單，保留庫存升冪。
5. `src/OrderHub.Web/ViewModels/LowStockViewModel.cs`（新增）— 綁定＋顯示合一；`[Range(1, int.MaxValue, ErrorMessage="門檻必須大於 0")] Threshold = 10` + `Products` 列。
6. `ProductsController.LowStock(LowStockViewModel query)`（修改）— `!ModelState.IsValid → return View(query)`；否則填 Products 後 `View(query)`。
7. `Views/Products/LowStock.cshtml`（新增）— GET form + `asp-validation-summary`，`table table-hover`，`<tr class="@(row.StockQuantity < 5 ? "table-danger" : "")">`，空清單 colspan=4。
8. `Views/Shared/_Layout.cshtml`（修改）— 加「低庫存」nav。
9. `tests/OrderHub.Tests/TestSetup.cs`（修改）— `CreateProductService` 改為 `new(new ProductRepository(db), new OrderRepository(db))`。
10. `tests/OrderHub.Tests/ProductLowStockTests.cs`（新增，≥3 測試）— 門檻過濾+升冪／排除停售／近 30 天售出排除 Cancelled+逾期。

## 驗證方式
1. `dotnet build`。
2. `dotnet test` 全綠（既有 31 + 新增 ≥3）。
3. `dotnet run`（:5150）實測：預設門檻 10、`?threshold=3` 變少、`?threshold=0/-1/abc` 顯示表單錯誤非 500、庫存 <5 紅底、售出欄排除 Cancelled。
4. `code-reviewer` subagent 檢視分層。
5. 一個獨立 commit。

> 原始 plan 檔：`~/.claude/plans/joyful-drifting-wilkinson.md`（本檔為 repo 內留存版）。
