namespace AgriAI.Web.ViewModels
{
    public class CartItemViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string? CategoryName { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public int StockQuantity { get; set; }
        public string? FarmerId { get; set; }
        public string? FarmerName { get; set; }
        public decimal TotalPrice => UnitPrice * Quantity;
        public decimal MaundPrice => UnitPrice * 20;
    }

    public class CartSummaryViewModel
    {
        public List<CartItemViewModel> Items { get; set; } = new List<CartItemViewModel>();
        public int TotalItemsCount => Items.Sum(i => i.Quantity);
        public decimal BaseTotal => Items.Sum(i => i.TotalPrice);
        public decimal PlatformFee => Math.Round(BaseTotal * 0.02m, 2);
        public decimal GrandTotal => BaseTotal + PlatformFee;
    }
}
