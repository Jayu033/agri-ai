using System.Collections.Generic;
using AgriAI.Web.Services;

namespace AgriAI.Web.ViewModels
{
    public class CropPredictorInputModel
    {
        // Simple Mode Inputs (For Farmers - Option A)
        public string Mode { get; set; } = "simple"; // "simple" or "advanced"
        public string District { get; set; } = "Rajkot";
        public string SoilType { get; set; } = "black"; // "black", "loamy", "sandy", "alluvial"
        public string WaterSource { get; set; } = "borewell"; // "borewell", "canal", "rainfed"
        public string WaterQuality { get; set; } = "fresh"; // "fresh" or "saline"
        public string PreviousCrop { get; set; } = "None"; // "None", "Groundnut", "Cotton", "Wheat", "Paddy", "Chana"
        public double CultivationArea { get; set; } = 5.0; // In Acres

        // Advanced Mode Inputs (Direct Lab Metrics)
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

        // Metadata Resolution for Simple Mode
        public string ResolvedDistrict { get; set; } = string.Empty;
        public string ResolvedSoilType { get; set; } = string.Empty;
        public string ResolvedWater { get; set; } = string.Empty;

        // Option B: Fertilizer Dosage Plan
        public FertilizerRecommendation FertilizerPlan { get; set; } = new();

        // Option F: Multi-Season Crop Rotation Calendar
        public CropRotationPlan CropRotation { get; set; } = new();
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
