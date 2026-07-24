# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 專案簡介

OrderHub：公司內部訂單管理系統。業務可建立/查詢訂單、管理商品與客戶。
內部使用、單一 SQL Server 資料庫，不需要多租戶或高併發架構——別套用比需求更複雜的做法。
（本 repo 是 AI Agent 培訓練習專案，部分行為刻意留有 bug，請讀程式碼求證，不要盡信描述。）

## 技術棧

- .NET 8 / ASP.NET Core MVC（Razor Views + 本地 Bootstrap 5，不依賴 CDN）
- EF Core 8 + SQL Server（本機安裝，不用 Docker）
- 測試：xUnit（EF Core InMemory，**不需要** SQL Server）

## 分層與慣例

三層，相依方向向內（`Web → Core ← Infrastructure`）：

- `OrderHub.Web`：Controller / View / ViewModel，只做接線與顯示
- `OrderHub.Core`：Domain models、service 介面與**所有商業邏輯**（折扣、庫存、狀態轉移）、repository 介面。不相依 EF Core
- `OrderHub.Infrastructure`：`OrderHubDbContext`、repository 實作、migrations、`DbSeeder`

規則：

- Controller 保持薄，只轉接 service 結果；商業邏輯一律放 Core 的 service
- 只有 repository 碰 `DbContext`；Controller / Service 不可直接用 EF Core
- Service 回傳 `ServiceResult<T>`，用它表達預期內的失敗，不要丟例外
- View 綁 ViewModel（手寫 mapping），不要把 domain model 丟給 View
- 使用者輸入用 DataAnnotations + ModelState 驗證；輸入錯誤絕不能變成 500
- 金額一律用 `decimal`；會員折扣集中在 `OrderService.CalculateTotal`（Standard ×1 / Silver ×0.95 / Gold ×0.90，總額折一次），不要在別處重算
- 建單時把單價快照進 `OrderItem.UnitPriceSnapshot`
- 操作結果訊息用 `TempData["Success"] / TempData["Error"]`（`_Layout.cshtml` 有共用 alert 區塊）
- 參考檔：Controller 照 `ProductsController.cs`、Service 照 `ProductService.cs` 的寫法

## 常用指令

於本目錄（`training-repo/`，`.sln` 所在層）執行：

- `dotnet build`：建置
- `dotnet test`：跑全部測試（InMemory，不碰資料庫）
- `dotnet test --filter "FullyQualifiedName~OrderServiceCreateTests"`：跑單一測試類別
- `dotnet run --project src/OrderHub.Web`：啟動網站（http://localhost:5150）
- 重置資料庫回種子：`dotnet ef database drop -f -p src/OrderHub.Infrastructure -s src/OrderHub.Web` 再 `dotnet run`

啟動時 `Program.cs` 會自動 `Migrate()` + `DbSeeder.SeedAsync`（固定 random seed，資料人人一致）。連線字串 key 為 `Default`，在 `src/OrderHub.Web/appsettings.Development.json`。

## 重要 / 危險檔案

- `src/OrderHub.Infrastructure/Migrations/**`：migration 是歷史紀錄，不要手改
- `src/OrderHub.Web/appsettings*.json`：連線字串等設定，改動前先問

## 不要做的事

- 不要未經同意就加新的 NuGet 套件
- 不要在 Controller / Service 直接使用 DbContext
- 不要為了「順手」重構與當前任務無關的程式碼
- 不要讀取或寫入任何機密檔（*.pfx、appsettings.Production.json、user-secrets）
