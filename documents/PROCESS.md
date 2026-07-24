# PROCESS.md — 我的練習心得

> 一個原則：**寫「具體發生的事」，不寫感想文。**
> 貼上當時真實的 prompt、真實的數字、真實的錯誤訊息——三個月後的你（和你的同事）才用得上。

#### 使用的 agent 與模型：Claude Code Opus 4.8 High

---

## 通用四問

### 1. 我的任務拆解

（開工前你把任務拆成哪幾步？實際做的時候順序有變嗎？為什麼變？）

- 原本規劃：① 讀懂專案 → ② 練習 1 設定 agent → ③ 練習 2 修 3 個 bug → ④ 練習 3 新功能 → ⑤ 練習 4 重構。
- 實際順序有變：做完練習 1（設定檔）後，我先插入一步「把 repo build 起來 + 建好資料庫」才進練習 2。原因是——bug 練習要求「先在頁面上重現」，沒有可跑的網站與種子資料根本無法重現，所以環境必須先就緒。
- 另一個變動：我原本讓 agent 直接 `/init` 產 CLAUDE.md，但發現它把檔案放錯位置（見第 3 題），所以多了一步「搬到正確目錄並依 agent-configuration 指南重寫」。

### 2. AI 幫上大忙的地方

（哪件事 agent 做得又快又好？**貼上當時的提問原文**，說明為什麼這樣問有效。）

- **環境設定與驗證一次到位**。提問原文：「setup the repo and db first」。這句雖短，但 agent 自己拆成：查 `dotnet --list-sdks` → 查 SQL Server 服務狀態 → 測 `localhost` 連線 → `dotnet build` → `dotnet run` 觸發自動 migrate+seed → 用 sqlcmd 核對種子筆數 → curl 打 `/Orders` 確認 HTTP 200。
- 為什麼有效：我沒有只問「幫我跑起來」，而是點名「repo **和 db**」，agent 就把「資料庫真的建好了嗎」當成獨立驗收項，主動去數 `Customers 20 / Products 50 / Orders 200 / OrderItems 501`（對得上 README 寫的 20/50/200），而不是看到 `Now listening` 就宣稱完成。
- 另一個好用點：讀程式碼定位慣例時，我要它「用 grep 求證宣稱的方法是否存在」，它抓到 `OrderService.CalculateTotal:138` 與 `GetDiscountRate:124` 的實際位置，讓 CLAUDE.md 寫的是事實而非猜測。

### 3. AI 誤導我的地方，與我如何發現

（agent 說錯／改錯／過度自信的時刻。你靠什麼抓到——對照程式碼？頁面實測？跑測試？）

- **CLAUDE.md 放錯位置**。`/init` 把檔案寫到 `repos\95-training\CLAUDE.md`，但這其實是最外層資料夾；真正的 git repo 根在 `repos\95-training\95-training\`（`.git` 在這層），而指南又要求 CLAUDE.md 要和 `.sln` 同層（即 `training-repo\`）。我靠 `git status` / 確認 `.git` 實際位置抓到——檔案根本不在版控範圍內，commit 也不會收錄。修正：搬到 `training-repo\CLAUDE.md`。
- **不能盡信「描述」**。第一版 CLAUDE.md 照 README 把折扣寫成「Gold ×0.9、總額折一次」當作現況。但實際讀碼發現：`CreateOrderAsync:78` 對 Gold 逐項先折一次，`CalculateTotal:138` 又對小計再折一次——**Gold 被折兩次**（正好是練習 2 客訴 2 的 bug）；另外 `CancelOrderAsync` 先把 `Status` 設成 `Cancelled`，才進 `if (Pending||Confirmed)` 判斷是否還原庫存，條件永遠為假、庫存從不回補（客訴 3）。
- 抓到的方法：不是相信 agent 的摘要，而是 `grep` 抽驗 + 逐行讀 `OrderService.cs`。教訓——agent 描述架構時傾向複述文件/README，真實行為要回程式碼對。

### 4. 我會帶回日常工作的一招

（一個具體、可複製的做法，不要寫「要多驗證」這種口號——寫出**操作步驟**。）

- **「產出後抽驗」三步**：只要 agent 產生了架構描述（CLAUDE.md、README、設計說明），就當場做——
  1. 挑它宣稱的 2～3 個具體事實（某方法名、某慣例、某檔路徑），逐一 `grep -rn` 求證存不存在、在哪一行。
  2. 對「行為型」宣稱（折扣怎麼算、狀態怎麼轉），直接開那個檔讀該段程式，別接受摘要。
  3. 對「檔案位置/版控」宣稱，用 `git status` 確認檔案真的被 repo 收錄、且在對的目錄。
- 成本很低（幾個 grep），但這次就靠它一次抓到「檔案放錯層」＋「折扣折兩次」兩個問題。

- **「回歸測試要先證明它會紅」**：修 bug 補測試後，不要只看「加了測試、全綠」就安心——那可能是測試根本沒測到 bug。操作步驟：
  1. 寫好測試、也寫好修正。
  2. `git stash push <只有修正的那個檔>` 把修正暫時收起，只留測試。
  3. 跑 `dotnet test --filter "FullyQualifiedName~<測試名>"`，**確認它 FAIL**，並核對 Expected/Actual 是不是你預期的錯法（如快照得 1278 而非 1420）。
  4. `git stash pop` 還原修正，再跑一次確認轉綠。
  這次三個 bug 都這樣做，才敢說回歸測試真的守得住。

## 自我驗證（做到哪個階段答哪題）

### 第一階段 — Agentic Coding

練習 1 — ✅ 完成

1. ✅ Web＝Controller/View/ViewModel（薄，只接線顯示）；Core＝Domain/Services/Interfaces（所有商業邏輯：折扣、庫存、狀態轉移）；Infrastructure＝DbContext/Repositories/Migrations/Seeder。相依方向 Web → Core ← Infrastructure。
2. ✅ 找到**兩處**不精確：README 說「Gold 折一次」，實際 `CreateOrderAsync` 與 `CalculateTotal` 各折一次共兩次；`CancelOrderAsync` 因設值順序導致還原庫存的條件恆為假。（見通用四問第 3 題）
3. ✅ 商業邏輯放 Core 的 service（透過 interface 注入）；新增頁面要動 Controller→Service(+Interface)→Repository→ViewModel→View(+導覽列)→測試六處（練習 3 會實作）。

> 設定產物：`training-repo/CLAUDE.md`、`.claude/settings.json`（permissions+hooks）、`.claude/agents/{code-reviewer,test-runner}.md`、`.claude/skills/fix-bug`、`.claude/hooks/*.ps1`；已 commit（`ba9028b`）。

練習 2 — ✅ 完成（3 bug／3 commit，`dotnet test` 31 全綠）

1. ✅ 三個 bug 都先重現，且記下真實數字：
   - 客訴 1：`/Orders` 第一頁 Id 為 `7,17,19…`，DB 最新是 Id `4,30,85`（最新 20 筆被跳過）；第 10 頁全空。
   - 客訴 2：Gold 客戶買 1420 元商品 × 1，明細頁應付顯示 **1150.20**，手算應為 1278。
   - 客訴 3：商品 #1 庫存 26 → 取消訂單 #201（qty 1）→ 庫存仍 **26**（沒加回）。
2. ✅ 給 agent 的都是具體數字（Id、1420/1278/1150.20、庫存 26→26），不是照貼客訴。
3. ✅ 修完都回頁面實測：page1 變 `4,30,85`＋第 10 頁 20 筆；Gold 明細變 **1,278.00**；建單庫存 102→100→取消回 **102**。
4. ✅ 三個回歸測試，且都先確認「修復前會 FAIL」：
   - `GetOrders_Page1_StartsAtNewestOrder_AndLastPageIsNotEmpty`（pre-fix：Items[0].Id 得 21 而非 1）
   - `CreateOrder_GoldCustomer_SnapshotsRawPrice_AndDiscountsTotalOnce`（pre-fix：快照得 1278 而非 1420）
   - `CancelOrder_RestoresProductStock`（pre-fix：庫存停在 7 而非 10）
5. ✅ 三個獨立 commit：`21580ca`（分頁 off-by-one）、`2c08af0`（Gold 折兩次）、`4f8f99f`（取消未還原庫存），message 皆「症狀→根因→修法」。
6. ✅ **思考題**：原測試都只驗「摘要屬性」而沒驗「經過會改狀態的路徑後的實際結果」——
   - Bug 1：舊測試只斷言 `TotalCount`/`TotalPages`，從不看 `Items` 內容。
   - Bug 2：pricing 測試只單獨測 `CalculateTotal`（本身正確），沒有一條走 `CreateOrderAsync`，而第二次折扣正藏在那裡。
   - Bug 3：cancel 測試只斷言狀態轉為 Cancelled，從不斷言庫存有沒有加回。
   共同教訓：**斷言「效果」而非「摘要」**，且要走真正的 service 路徑，不要只單測純函式。

練習 3 — ✅ 完成（先計畫後實作，1 commit `cbe49c4`，`dotnet test` 34 全綠）

做法：用 Plan Mode——agent 只讀檔規劃、不改任何檔，先派 Explore 子代理把既有 Products 垂直切面（Controller/Service/Repository/ViewModel/View/測試）全部盤點，我核對計畫後才放行實作。計畫存於 `documents/plans/exercise-3-lowstock-plan.md`。

1. ✅ 頁面實測（curl 打 :5150）：不帶參數 → 門檻 10 回 **5** 個商品、依庫存升冪（2,3,3,4,4）；`?threshold=3` → 縮到 **1** 個。
2. ✅ `?threshold=0`、`-1`、甚至 `abc` → HTTP **200**（非 500），頁面出現 `validation-summary-errors`「門檻必須大於 0」，商品清單為空。（機制：`[Range(1,int.MaxValue)]` + `ModelState.IsValid` → `return View(query)`）
3. ✅ 「近 30 天售出」欄有真實數字（如某商品 22、26），且測試證明排除 Cancelled 與逾 30 天訂單（同商品 25 天前 Confirmed 2 + Cancelled 5 + 40 天前 7 → 只算 **2**）。
4. ✅ 停售商品不出現（測試 `GetLowStock_ExcludesInactiveProducts` + repo `Where(p.IsActive && ...)`）。
5. ✅ 分層乾淨：邏輯（30 天視窗、合併）在 `ProductService`，EF 查詢在兩個 repository（一次 GROUP BY、無 N+1），Controller 只映射，View 綁 `LowStockViewModel`。跑了一輪 reviewer 子代理確認。
6. ✅ 4 個 service 測試全綠（門檻過濾＋邊界＋升冪／排除停售／近 30 天排除 Cancelled）。

⚠️ **reviewer 抓到我漏測的邊界**：第一版門檻測試用庫存 3/8/20 對門檻 10，**沒有「剛好等於 10」的樣本**——就算把 `<` 改成 `<=` 也會全綠（測不到 bug）。補了 stock=10 應排除的樣本，並用「先證明會紅」法確認：把 repo 改成 `<=` 跑該測試 → FAIL（回 3 而非 2），改回 `<` → 綠。教訓：邊界條件的測試一定要放一個「剛好在界線上」的樣本。

練習 4 — ✅ 完成（先計畫後執行，1 commit `a0ae2c5`）

做法：用 writing-plans skill 產出計畫（`documents/plans/2026-07-24-refactor-createorder-validation.md`），選 inline 執行——先跑基準測試 34 綠，再一次原子改動，再跑一次確認仍 34 綠。

1. ✅ 重構後 `dotnet test` 34 全綠，**且未改任何測試檔**（這才是「行為不變」最強的證據——重構若動到測試，等於偷改了契約）。
2. ✅ 改善／不變：
   - 改善：`CreateOrderAsync` 從 ~55 行「驗證＋建單＋扣庫存＋持久化」混在一起，縮成薄編排；驗證規則抽成兩個具名純方法 `ValidateHeader`（客戶／明細非空／數量／重複商品）與 `ValidateLine`（存在啟用／庫存），「什麼樣的訂單不合法」讀兩個短方法就懂。
   - 不變：每一則錯誤訊息、判斷順序、header 先失敗 vs 逐行累積的語意、public 簽章、DI 圖。
3. ✅ 我看過 diff（只有 `OrderService.cs`，+32/−20），確認純抽方法、訊息與順序原樣；`customer!`/`product!` 只在驗證已擋掉 null 後才用，安全。沒有「agent 說好就好」。

思考：這次沒有另外抽 `IOrderValidator` 介面/類別——只有一個呼叫端，介面是為想像中的第二個實作而寫（YAGNI）。私有方法就達成目標，這也是一種「不過度設計」的判斷。

---

## 附錄：值得留下的對話片段

（貼 1–2 段最有代表性的 prompt 與回應**摘要**——不用貼全文，重點是「我怎麼問」和「它怎麼答」。）

**片段 1 — 用短指令換到完整驗收**
- 我問：「setup the repo and db first」
- 它答（摘要）：沒有直接 `dotnet run` 了事，而是先查 SDK/SQL 服務/連線，build 後 run 觸發 migrate+seed，再用 sqlcmd 回報 `Customers 20 / Products 50 / Orders 200 / OrderItems 501` 並 curl `/Orders` 得 HTTP 200。學到：把驗收目標（「db」）明確寫進指令，agent 會自己補上核對步驟。

**片段 2 — 質疑而非照收**
- 我（心裡）問：CLAUDE.md 寫的折扣規則是真的嗎？
- 它照 README 寫「Gold 折一次」，但我要它 grep+讀碼求證後，改口指出 `CreateOrderAsync:78` 與 `CalculateTotal:138` 會讓 Gold 折兩次。學到：架構描述要回程式碼對，README 只是「應該」，不是「現在」。

**片段 3 — 用「先重現的真實數字」驅動修 bug**
- 我給的不是「幫我修分頁 bug」，而是：「DB 最新訂單是 Id 4/30/85，但 `/Orders` 第一頁只有 7/17/19…，最新的都不見了；第 10 頁全空。」
- agent 直接指到 `OrderRepository.cs:32` 的 `Skip(page * pageSize)`（page 是 1-based，第一頁就跳掉最新 20 筆），一行改成 `Skip((page-1)*pageSize)`。學到：帶著具體數字進場，定位從「猜」變成「對號入座」。

**片段 4 — 先計畫、reviewer 補刀**
- 練習 3 我要它「先不要寫程式，給實作計畫並派子代理盤點既有 Products 慣例」，核對後才放行。實作完再叫它以 reviewer 角度看 diff。
- reviewer 回：架構乾淨，但「門檻測試沒有等於門檻的樣本，`<` 改 `<=` 也會過」。我照補 stock=10 樣本並證明它會紅。學到：agent 自己寫的測試也要被另一個 agent（或自己）挑，「測試有沒有真的能抓到 bug」比「測試綠不綠」重要。
