using System.Collections.Generic;

namespace AgriAI.Web.ViewModels
{
    public class DiseasePredictionResult
    {
        public string CropName { get; set; } = string.Empty;
        public string GujaratiCropName { get; set; } = string.Empty;
        public string DiseaseName { get; set; } = string.Empty;
        public string GujaratiDiseaseName { get; set; } = string.Empty;
        public string Severity { get; set; } = "Moderate"; // Low, Moderate, High, Severe
        public string SeverityBadgeClass { get; set; } = "bg-warning text-dark";
        public double Confidence { get; set; } = 95.0;
        public string Status { get; set; } = "Infected"; // Healthy, Infected
        public string Symptoms { get; set; } = string.Empty;
        public string SymptomsGu { get; set; } = string.Empty;
        public string Cause { get; set; } = string.Empty;
        public string CauseGu { get; set; } = string.Empty;
        public List<string> ChemicalRemedies { get; set; } = new();
        public List<string> ChemicalRemediesGu { get; set; } = new();
        public List<string> OrganicRemedies { get; set; } = new();
        public List<string> OrganicRemediesGu { get; set; } = new();
        public List<string> PreventiveTips { get; set; } = new();
        public List<string> PreventiveTipsGu { get; set; } = new();
        public string? ImagePreviewUrl { get; set; }
        public string Emoji { get; set; } = "🌿";
    }
}
