# Refactor CreateOrderAsync Validation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract the tangled inline validation inside `OrderService.CreateOrderAsync` into two small pure helper methods, with behavior 100% unchanged and all existing tests still green.

**Architecture:** `CreateOrderAsync` today mixes four "header" guard checks (customer exists, lines non-empty, positive quantity, no duplicate product) and two "per-line" checks (product exists/active, sufficient stock) in with the order-building/side-effect logic (stock decrement, item add, persist). We pull validation out into two `private static` pure functions — `ValidateHeader` (returns the single first error, or null) and `ValidateLine` (returns the per-line error, or null) — leaving `CreateOrderAsync` as thin orchestration. No new types, no new dependencies, no interface change. Pure extract-method refactor; the existing test suite is the safety net.

**Tech Stack:** .NET 8, C#, xUnit (EF Core InMemory). Commands run from `training-repo/`.

**Why not a separate Validator class:** there is exactly one caller and one order-creation flow. A `IOrderValidator` interface + class would be speculative abstraction (YAGNI); private methods on the service are the minimal change that meets the goal.

---

## Pre-flight (run once before Task 1)

- [ ] **Stop the dev server** so the build isn't blocked by a locked DLL.

Run:
```bash
powershell -NoProfile -Command "(Get-NetTCPConnection -LocalPort 5150 -State Listen -ErrorAction SilentlyContinue).OwningProcess | Sort-Object -Unique | ForEach-Object { try { Stop-Process -Id \$_ -Force } catch {} }"
```
Expected: no error (kills the server if running, silent if not).

---

### Task 1: Establish the green baseline

**Files:** none (verification only)

- [ ] **Step 1: Run the full suite to confirm the safety net is green before touching anything**

Run: `dotnet test --nologo` (from `training-repo/`)
Expected: `Passed! - Failed: 0, Passed: 34, Skipped: 0, Total: 34`

If it is not 34/34 green, STOP — do not refactor on top of a red baseline.

---

### Task 2: Extract `ValidateHeader` and `ValidateLine`, rewrite `CreateOrderAsync`

**Files:**
- Modify: `src/OrderHub.Core/Services/OrderService.cs` (the `CreateOrderAsync` method, lines 35–92, and add two private methods after it)

This task is a single atomic edit (a refactor has no intermediate "failing test" — the existing tests define the contract). Show the whole new shape.

- [ ] **Step 1: Replace the body of `CreateOrderAsync`**

Replace the current method (from `public async Task<ServiceResult<Order>> CreateOrderAsync` through its closing `}` at the `return ServiceResult<Order>.Ok(order);` block) with:

```csharp
    public async Task<ServiceResult<Order>> CreateOrderAsync(int customerId, IReadOnlyList<NewOrderLine> lines)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);

        var headerError = ValidateHeader(customer, lines);
        if (headerError is not null)
            return ServiceResult<Order>.Fail(headerError);

        var order = new Order
        {
            CustomerId = customer!.Id,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var errors = new List<string>();
        foreach (var line in lines)
        {
            var product = await _productRepository.GetByIdAsync(line.ProductId);

            var lineError = ValidateLine(product, line);
            if (lineError is not null)
            {
                errors.Add(lineError);
                continue;
            }

            product!.StockQuantity -= line.Quantity;

            // 單價快照存「原價」；會員折扣一律只在 CalculateTotal 對總額折一次，
            // 不要在這裡先折（否則 Gold 會被折兩次）。
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = line.Quantity,
                UnitPriceSnapshot = product.UnitPrice
            });
        }

        if (errors.Count > 0)
            return ServiceResult<Order>.Fail(errors);

        await _orderRepository.AddAsync(order);
        await _orderRepository.SaveChangesAsync();

        return ServiceResult<Order>.Ok(order);
    }
```

- [ ] **Step 2: Add the two pure validation helpers immediately after `CreateOrderAsync`** (before `CancelOrderAsync`)

```csharp
    // 純驗證：不做任何副作用，回傳第一個錯誤訊息，通過則回 null。
    private static string? ValidateHeader(Customer? customer, IReadOnlyList<NewOrderLine> lines)
    {
        if (customer is null)
            return "找不到指定的客戶";
        if (lines is null || lines.Count == 0)
            return "訂單至少需要一項商品";
        if (lines.Any(l => l.Quantity <= 0))
            return "商品數量必須大於 0";
        if (lines.Select(l => l.ProductId).Distinct().Count() != lines.Count)
            return "同一商品請勿重複加入，請調整數量即可";
        return null;
    }

    private static string? ValidateLine(Product? product, NewOrderLine line)
    {
        if (product is null || !product.IsActive)
            return $"商品（Id={line.ProductId}）不存在或已停售";
        if (product.StockQuantity < line.Quantity)
            return $"商品「{product.Name}」庫存不足（現有 {product.StockQuantity}，需求 {line.Quantity}）";
        return null;
    }
```

Notes for the implementer (do NOT change behavior):
- The check **order and exact messages** are copied verbatim — customer → empty → quantity → duplicate (header); product null/inactive → stock (per-line). Any reordering or wording change is a behavior change and a bug.
- `customer!` and `product!` null-forgiving operators are safe: `ValidateHeader`/`ValidateLine` already returned on the null case, so control only reaches them when non-null.
- Side effects (`product.StockQuantity -=`, `order.Items.Add`, persist) stay in `CreateOrderAsync`. The helpers are pure — that is the point of the split.

- [ ] **Step 3: Build**

Run: `dotnet build -v q --nologo` (from `training-repo/`)
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

---

### Task 3: Verify behavior unchanged and commit

**Files:** none (verification + commit)

- [ ] **Step 1: Run the full suite — must be identical to the baseline**

Run: `dotnet test --nologo`
Expected: `Passed! - Failed: 0, Passed: 34, Skipped: 0, Total: 34`

All 34 tests must pass with zero changes to any test file. If any test fails, the refactor changed behavior — revert and reconcile, do not "fix" the test.

- [ ] **Step 2: Review the diff yourself (and optionally via a reviewer agent)**

Run: `git -C C:/Users/dm95/source/repos/95-training/95-training diff -- training-repo/src/OrderHub.Core/Services/OrderService.cs`
Confirm: only `CreateOrderAsync` was restructured + two private methods added; messages/branch order unchanged; no other method touched.

- [ ] **Step 3: Commit**

```bash
cd /c/Users/dm95/source/repos/95-training/95-training
git add training-repo/src/OrderHub.Core/Services/OrderService.cs
git commit -m "refactor(orders): extract CreateOrderAsync validation into pure helpers

把 CreateOrderAsync 內糾纏的驗證抽成兩個純方法 ValidateHeader（客戶存在／明細非空／
數量／重複商品）與 ValidateLine（商品存在啟用／庫存），主流程只剩編排（建單、扣庫存、
持久化）。行為完全不變、訊息與判斷順序原樣保留，34 測試全綠。無新型別、無介面變動。

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## What improved / what did not change (for PROCESS.md 練習 4 #2)

- **Improved:** `CreateOrderAsync` shrinks from ~55 lines of mixed concerns to thin orchestration; validation rules are now two named, pure, individually-readable functions; "what makes an order invalid" is answerable by reading two short methods.
- **Unchanged:** every error message, the order of checks, the accumulate-per-line vs fail-fast-on-header semantics, the public signature, and the DI graph. Proven by the 34-test suite staying green with no test edits.

## Verification (end-to-end)
1. `dotnet test` → 34/34 green with **no test file modified** (the strongest evidence for a behavior-preserving refactor).
2. `git diff` shows only `OrderService.cs` changed.
3. Optional smoke test: `dotnet run --project src/OrderHub.Web` (:5150), create a valid order and an invalid one (e.g. duplicate product) via `/Orders/Create` — same success/error behavior as before.
