using AgriAI.Domain.Common;

namespace AgriAI.Domain.Entities
{
    public class Review : BaseEntity
    {
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
        
        public string CustomerId { get; set; } = string.Empty;
        
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string SentimentPrediction { get; set; } = string.Empty; // Positive/Negative from ML.NET
    }
}
