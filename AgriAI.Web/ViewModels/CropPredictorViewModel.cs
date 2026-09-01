namespace AgriAI.Web.ViewModels
{
    public class CropPredictorInputModel
    {
        public double Nitrogen { get; set; } = 90;
        public double Phosphorus { get; set; } = 42;
        public double Potassium { get; set; } = 43;
        public double Temperature { get; set; } = 20.8;
        public double Humidity { get; set; } = 82.0;
        public double PhValue { get; set; } = 6.5;
        public double Rainfall { get; set; } = 150.0;
    }

    public class CropPredictionResult
    {
        public string CropName { get; set; } = string.Empty;
        public string GujaratiName { get; set; } = string.Empty;
        public string Emoji { get; set; } = "🌱";
        public string Category { get; set; } = "Grain";
        public double MatchScore { get; set; }
        public string Season { get; set; } = "Kharif / Rabi";
        public string ExpectedYield { get; set; } = "High";
        public string GrowthDuration { get; set; } = "90-120 Days";
        public double EstimatedPricePerQtl { get; set; }
        public string Description { get; set; } = string.Empty;
        public string SoilAdvice { get; set; } = string.Empty;
        public List<CropAlternative> TopAlternatives { get; set; } = new();
        public CropPredictorInputModel Input { get; set; } = new();
    }

    public class CropAlternative
    {
        public string CropName { get; set; } = string.Empty;
        public string Emoji { get; set; } = "🌿";
        public double MatchScore { get; set; }
        public string Category { get; set; } = string.Empty;
    }

    public class PricePredictorInputModel
    {
        public string CropName { get; set; } = "Wheat";
        public string Season { get; set; } = "Rabi";
        public double Rainfall { get; set; } = 450.0;
        public double Area { get; set; } = 5.0;
    }
}
