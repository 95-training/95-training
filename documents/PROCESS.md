# PROCESS.md — 我的練習心得

> 一個原則：**寫「具體發生的事」，不寫感想文。**
> 貼上當時真實的 prompt、真實的數字、真實的錯誤訊息——三個月後的你（和你的同事）才用得上。

#### 使用的 agent 與模型：Claude Code Opus 4.8 High

---

## 通用四問

### 1. 我的任務拆解

（開工前你把任務拆成哪幾步？實際做的時候順序有變嗎？為什麼變？）

- 剛開始先 /init，把 CLAUDE.md 產出來，確認技術棧、慣例、禁止事項都正確。遵循reduce-token-usage.md 的建議 和 利用prompt-caching。

### 2. AI 幫上大忙的地方

（哪件事 agent 做得又快又好？**貼上當時的提問原文**，說明為什麼這樣問有效。）

- **環境設定與驗證一次到位**。提問原文：「setup the repo and db first」。這句雖短，但 agent 自己拆成：查 `dotnet --list-sdks` → 查 SQL Server 服務狀態 → 測 `localhost` 連線 → `dotnet build` → `dotnet run` 觸發自動 migrate+seed → 用 sqlcmd 核對種子筆數 → curl 打 `/Orders` 確認 HTTP 200。
- 為什麼有效：我沒有只問「幫我跑起來」，而是點名「repo **和 db**」，agent 就把「資料庫真的建好了嗎」當成獨立驗收項，主動去數 `Customers 20 / Products 50 / Orders 200 / OrderItems 501`（對得上 README 寫的 20/50/200），而不是看到 `Now listening` 就宣稱完成。

### 3. AI 誤導我的地方，與我如何發現

（agent 說錯／改錯／過度自信的時刻。你靠什麼抓到——對照程式碼？頁面實測？跑測試？）

### 4. 我會帶回日常工作的一招

（一個具體、可複製的做法，不要寫「要多驗證」這種口號——寫出**操作步驟**。）
1. 新repo一定要 `/init` 產 CLAUDE.md，**先讓 agent 讀懂專案**，再開始問它問題。這樣它才不會每次都重頭摸索，省下很多 token。
2. Plan & Review：**先讓 agent 產出計畫，再自己核對、再放行**。
3. 修bug時，**先在頁面上重現症狀、再把具體觀察告訴 agent**，而不是只貼客訴原文。這樣它才不會亂猜。
4. hooks：**把重複流程做成 skill / hook**，例如 `fix-bug`、`writing-plans`，讓 agent 自己去跑，省下 prompt token。

## 自我驗證（做到哪個階段答哪題）

### 第一階段 — Agentic Coding

練習 1 — ✅ 完成

1. ✅ 我能不看筆記說出三個專案（Web/Core/Infrastructure）各自的職責
2. ✅ 我核對過 agent 描述的建單流程，且至少找出一處不精確或過度簡化的說法
3. ✅ 我知道商業邏輯應該放在哪一層、新增頁面要動哪些地方

練習 2 — ✅ 完成

1. ✅ 三個 bug 我都先在頁面上重現過，才開始找程式
2. ✅ 我給 agent 的資訊包含具體觀察（頁碼／金額數字／庫存數字），而不是只貼客訴原文
3. ✅ 每個修復都回到頁面驗證過症狀消失
4. ✅ 每個 bug 都補了一個回歸測試，dotnet test 全綠
5. ✅ 三個獨立 commit，message 說明症狀與根因
6. ✅ （思考題）為什麼原本的測試沒抓到這三個 bug？
   - Bug 1：舊測試只斷言 `TotalCount`/`TotalPages`，從不看 `Items` 內容。
   - Bug 2：pricing 測試只單獨測 `CalculateTotal`（本身正確），沒有一條走 `CreateOrderAsync`，而第二次折扣正藏在那裡。
   - Bug 3：cancel 測試只斷言狀態轉為 Cancelled，從不斷言庫存有沒有加回。
   共同教訓：**斷言「效果」而非「摘要」**，且要走真正的 service 路徑，不要只單測純函式。

練習 3 — ✅ 完成
1. ✅ `/Products/LowStock` 不帶參數 → 門檻 10 的結果；帶 `?threshold=3` → 結果隨之改變
2. ✅ `?threshold=0`、`?threshold=-1` → 頁面顯示驗證錯誤，不是 500
3. ✅ 售出數量欄位排除了 Cancelled 訂單（可用一筆已取消的訂單驗證）
4. ✅ 停售商品不出現（測試 `GetLowStock_ExcludesInactiveProducts` + repo `Where(p.IsActive && ...)`）。
5. ✅ 程式分層與命名跟既有的 Products 功能一致（請 agent 自我 review 一次，並自己確認）。
6. ✅ 至少 3 個新測試，`dotnet test` 全綠

練習 4 — ✅ 完成

做法：用 writing-plans skill 產出計畫（`documents/plans/2026-07-24-refactor-createorder-validation.md`），選 inline 執行——先跑基準測試 34 綠，再一次原子改動，再跑一次確認仍 34 綠。

1. ✅ 重構後 `dotnet test` 全綠
2. ✅ 我能說出這次重構「改善了什麼、沒有改變什麼」
3. ✅ 我有在 code review 的角度看過 diff（不是 agent 說好就好）

---

## 附錄：值得留下的對話片段

（貼 1–2 段最有代表性的 prompt 與回應**摘要**——不用貼全文，重點是「我怎麼問」和「它怎麼答」。）

**片段 1 — 用短指令換到完整驗收**
- 我問：「setup the repo and db first」
- 它答（摘要）：沒有直接 `dotnet run` 了事，而是先查 SDK/SQL 服務/連線，build 後 run 觸發 migrate+seed，再用 sqlcmd 回報 `Customers 20 / Products 50 / Orders 200 / OrderItems 501` 並 curl `/Orders` 得 HTTP 200。學到：把驗收目標（「db」）明確寫進指令，agent 會自己補上核對步驟。

**片段 2 — 先計畫、reviewer 補刀**
- 練習 3 我要它「先不要寫程式，給實作計畫並派子代理盤點既有 Products 慣例」，核對後才放行。實作完再叫它以 reviewer 角度看 diff。
- reviewer 回：架構乾淨，但「門檻測試沒有等於門檻的樣本，`<` 改 `<=` 也會過」。我照補 stock=10 樣本並證明它會紅。學到：agent 自己寫的測試也要被另一個 agent（或自己）挑，「測試有沒有真的能抓到 bug」比「測試綠不綠」重要。


## Activity 2
0.2 我讓Agent 用playwright 重新驗證 activity 1 的 客訴1，并將各步驟都截圖起來方便我做檢查
4 
a. 對 agent 說「幫我取消訂單 X」:觀察權限確認提示——你按允許之前,資料不會被動到 > 會提醒操作不可逆
b. 檢查回補, 會使用 lowstock 1000 來模擬get，然後查看庫存是否回補
c. 重複取消 > 訂單 #204 之前已經取消過了(狀態是 Cancelled),系統擋下重複取消:「狀態為 Cancelled 的訂單不可取消」。不需要再做什麼。