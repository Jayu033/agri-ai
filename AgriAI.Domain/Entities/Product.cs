using AgriAI.Domain.Common;

namespace AgriAI.Domain.Entities
{
    public class Product : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        
        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        public string FarmerId { get; set; } = string.Empty;
        
        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
