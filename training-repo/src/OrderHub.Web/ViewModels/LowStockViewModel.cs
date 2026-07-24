using System.ComponentModel.DataAnnotations;

namespace OrderHub.Web.ViewModels;

public class LowStockViewModel
{
    // 未帶 threshold → 預設 10；<= 0（或非數字綁定失敗）→ ModelState 無效 → 顯示表單錯誤，不是 500。
    [Range(1, int.MaxValue, ErrorMessage = "門檻必須大於 0")]
    [Display(Name = "庫存門檻")]
    public int Threshold { get; set; } = 10;

    public IReadOnlyList<LowStockRowViewModel> Products { get; set; } = Array.Empty<LowStockRowViewModel>();
}

public class LowStockRowViewModel
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public int SoldLast30Days { get; set; }
}
