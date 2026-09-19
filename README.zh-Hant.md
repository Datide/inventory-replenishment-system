# C# / .NET 範例 — 庫存補貨系統

> **🌐 語言：** [English](README.md) | [繁體中文](README.zh-Hant.md)

> **重要聲明：本專案為 Datide 製作的作品集示範。**
> 應用程式內的業務情境、公司名稱與資料均為虛構——不含任何真實客戶程式碼或機密資料。

一套全端 .NET 10 庫存補貨系統，具備角色登入、SKU 管理、採購單流程
（草稿 → 提交 → 主管審批）、重複單防護、超量／不足訂購警告、**庫存上限審批
防護**，以及**三語介面（English / 繁體中文 / 简体中文）**，另附 Excel 匯出。
所有金額一律以 **USD** 顯示。

## 它展示了甚麼（為何有賣點）

這不是「給你看程式碼」的範例——**它是一套客戶可以實際點擊操作的系統**：

| 客戶痛點 | 系統如何解決 |
|---|---|
| 「成日缺貨」 | 儀表板執行補貨檢查 → 建議哪些 SKU 要補、補幾多 |
| 「重複開採購單」 | 同一個 SKU **不可以**同時有兩張進行中的 PO（草稿或待審批）——應編輯現有那張 |
| 「員工開錯數量」 | 員工可在 **提交審批前** 檢視並編輯 PO（草稿流程） |
| 「冇人發現超量訂購」 | 數量高於系統建議值會以**黃色**標示——員工列表和主管審批畫面都見到 |
| 「主管批咗張超出庫存上限嘅單」 | 當「目前庫存 + 已訂未入庫」會超過該 SKU 的 MaxStock 時，**審批會被封鎖**——Approve 按鈕停用、伺服器亦會拒絕請求 |
| 「唔知邊個開嘅單」 | 每張 PO 記錄建立者、提交者、審批／拒絕者（稽核軌跡） |
| 「訂單需要主管簽核」 | 員工建立 PO → **主管專屬**審批頁 → 附理由批准／拒絕 |
| 「多人同時開單會亂」 | GUID 訂單編號（不可能重複）＋ 每 SKU 一張進行中 PO |
| 「團隊睇中文、客戶睇英文」 | 整個介面可經選單列語言選擇器切換 **English、繁體中文、简体中文**（Cookie 記憶） |
| 「想用 Excel 覆核資料」 | 一鍵匯出所有資料表為 .xlsx |

## 範圍說明 — 此示範刻意不包含的內容

**入貨（Inbound receiving）與出貨（Outbound dispatch）不在本範例範圍內。**
它們屬於另一套流程：

- **入貨**屬於 *驗收／收貨* 流程（核對訂單內容與實際到貨是否一致）——與庫存水位決策是不同工作流程。
- **出貨**屬於 *包裝／履行* 流程（揀貨、包裝、出貨給客戶）——同樣是獨立流程。

本示範專注於**庫存管理決策迴圈**：掌握目前水位、決定補貨數量、並管控採購單
流程直到審批為止。由於不含入貨／出貨，`StockSnapshot` 水位視為已知輸入
（種子資料），不會因收貨或出貨事件而變動。若想完整閉環，收貨模組會是
自然的下一步擴充。

## 執行

```bash
dotnet run --project src\Datide.Replenishment.Web
# 開啟 http://localhost:5000  （launchSettings 可能設定為 5165）
```

零設定——首次執行會自動建立並填入 SQLite 資料庫。

**示範帳戶：**

| 角色 | 帳號 | 密碼 |
|---|---|---|
| 經理（可審批） | `manager` | `Manager@123` |
| 職員（建立 PO） | `staff` | `Staff@123` |

**試玩流程：** 以 `staff` 登入 → 開啟語言選擇器切換到**繁體中文** → 為
SKU-1001 建立 PO → 它在編輯頁以**草稿**開啟 → 將數量改為 `250`（高於系統建議值——
欄位會變**黃色**）→ 提交審批 → 再為同一個 SKU 建立另一張 PO（會被封鎖）→
登出 → 以 `manager` 登入 → 審批頁：你會見到黃色的超量警告，以及紅色的
**「超出庫存上限」**封鎖（目前庫存 8 + 已訂未入庫 250 = 258 > 上限 200），
Approve 按鈕已停用 → 切換語言回 **English**，觀察所有文字即時翻譯。

## 解決方案結構

```
Datide.Replenishment.slnx
├── src/
│   ├── Datide.Replenishment.Domain/         # 純業務邏輯，零依賴
│   │   ├── Entities/        StockSnapshot, Sku, User, PurchaseOrder, ReplenishmentSuggestion
│   │   ├── Interfaces/      IReplenishmentRule
│   │   └── Rules/           ConsecutiveDaysReplenishmentRule
│   ├── Datide.Replenishment.Application/    # 使用案例＋契約
│   │   ├── Abstractions/    IStockSnapshotRepository, IPurchaseOrderRepository
│   │   ├── Contracts/       EvaluateSkuRequest, EvaluationResult
│   │   └── Services/        ReplenishmentService
│   ├── Datide.Replenishment.Infrastructure/ # EF Core + SQLite + 種子資料
│   │   └── Persistence/     DbContext, Repositories, DatabaseSeeder
│   ├── Datide.Replenishment.Web/            # Razor Pages + Cookie 驗證 + i18n
│   │   ├── Pages/
│   │   │   ├── Account/     Login, Logout, AccessDenied
│   │   │   ├── Skus/        List, Create, Edit
│   │   │   └── PurchaseOrders/ List, Create, Edit (草稿), Approvals (主管限定), Export
│   │   ├── Resources/       SharedResource.resx（＋ zh-Hant / zh-Hans satellite）
│   │   └── App_Data/        datide.db（自動建立）
└── tests/
    └── Datide.Replenishment.UnitTests/      # 15 個測試 — 規則引擎＋服務＋in-flight 邏輯
```

## 值得在面試中提出的設計決策

| 決策 | 原因 |
|---|---|
| **SQLite＋Repository 抽象** | 零設定示範；日後只需更換 provider／連線字串即可轉 SQL Server——Application 層從不直接觸碰 EF Core |
| **草稿 → 提交 → 審批 生命週期** | 員工可在單據送達主管前檢視與修正；一旦提交即鎖定 |
| **重複單防護** | 每個 SKU 只能有一張進行中的 PO（草稿或待審批）——以編輯取代重複開單 |
| **In-flight 庫存感知** | 補貨建議會扣除已訂未入庫數量（草稿／待審批／已批准）——系統絕不會建議重複訂購 |
| **超量／不足警告** | 每張 PO 在建立時快照系統建議數量；高於此值以黃色標示（員工列表與主管畫面皆然）；低於此值會要求員工確認 |
| **庫存上限審批防護** | 審批前伺服器檢查 `目前庫存 + in-flight > SKU.MaxStock`；超限時 Approve 按鈕停用、POST 亦被拒絕——作為最後防線，確保超限訂單無法漏過 |
| **與語言無關的 Domain 層** | 規則回傳*結構化事實*（庫存水位、總需求、in-flight），而非英文句子——由 UI 層以 localizer 組句，因此每種語言都能得到正確句子，毋須剖析文字 |
| **三語介面（i18n）** | `IStringLocalizer<SharedResource>`＋neutral/zh-Hant/zh-Hans resx，透過 `/set-language` 以 Cookie 記憶；不受瀏覽器自動偵測干擾 |
| **GUID 訂單編號** | 並發使用者絕不可能拿到相同編號（有別於流水號計數器） |
| **採購單稽核欄位** | RequestedBy/At、SubmittedAt、DecidedBy/At/Reason——永遠答得到「邊個做咗咩」 |
| **SKU 軟停用** | 從不硬刪除；歷史紀錄保持一致 |
| **密碼雜湊** | 以 ASP.NET Core `PasswordHasher` 產生——絕不存明文 |
| **ClosedXML Excel 匯出** | 不接觸系統的利害關係人也能覆核資料 |
| **以不變文化強制 USD** | 無論主機地區設定為何，貨幣一律顯示 USD |

## 建置與測試

```bash
dotnet build Datide.Replenishment.slnx
dotnet test  Datide.Replenishment.slnx
```

**端到端實測確認：** 0 建置警告／錯誤 · 15 個單元測試全過 · 登入、角色隔離
（職員無法進入審批頁）、草稿 → 編輯 → 提交 → 審批流程、重複單防護、
in-flight 補貨計算、黃色超量警告一路帶到主管畫面、**庫存上限審批封鎖**
（介面停用＋伺服器拒絕）、**三語切換 EN / 繁體中文 / 简体中文**（含補貨
原因句子的翻譯），以及 Excel 匯出——全部已對運行中的應用程式實測。

---

© 2026 Datide. 版權所有。

本專案為作品集展示用途，程式碼僅供閱覽，未經書面許可不得複製、修改或轉載。
