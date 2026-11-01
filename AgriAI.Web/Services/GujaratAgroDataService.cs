using System;
using System.Collections.Generic;
using System.Linq;

namespace AgriAI.Web.Services
{
    public interface IGujaratAgroDataService
    {
        List<DistrictAgroProfile> GetAllDistricts();
        DistrictAgroProfile? GetDistrict(string name);
        SoilBenchmarkData ResolveSoilAndClimate(string districtName, string soilType, string waterSource, string waterQuality, string? previousCrop);
        FertilizerRecommendation CalculateFertilizer(string cropName, double n, double p, double k, double areaAcres);
        CropRotationPlan GetCropRotationPlan(string primaryCrop);
    }

    public class DistrictAgroProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty; // Saurashtra, North Gujarat, Central Gujarat, South Gujarat, Kutch
        public string AgroClimaticZone { get; set; } = string.Empty;
        public double AvgRainfallMm { get; set; }
        public double AvgTempC { get; set; }
        public double AvgHumidity { get; set; }
        public double BaselineN { get; set; }
        public double BaselineP { get; set; }
        public double BaselineK { get; set; }
        public double BaselinePh { get; set; }
        public string DefaultSoilType { get; set; } = "loamy";
        public List<string> ProminentCrops { get; set; } = new();
    }

    public class SoilBenchmarkData
    {
        public double Nitrogen { get; set; }
        public double Phosphorus { get; set; }
        public double Potassium { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public double PhValue { get; set; }
        public double Rainfall { get; set; }
        public string DistrictName { get; set; } = string.Empty;
        public string SoilTypeName { get; set; } = string.Empty;
        public string WaterSummary { get; set; } = string.Empty;
    }

    public class FertilizerRecommendation
    {
        public double UreaKgPerAcre { get; set; }
        public double UreaKgPerVigha { get; set; }
        public double DapKgPerAcre { get; set; }
        public double DapKgPerVigha { get; set; }
        public double PotashKgPerAcre { get; set; }
        public double PotashKgPerVigha { get; set; }
        public string SowingBaseDose { get; set; } = string.Empty;
        public string TopDressingStage1 { get; set; } = string.Empty;
        public string TopDressingStage2 { get; set; } = string.Empty;
        public string BioFertilizerAdvice { get; set; } = string.Empty;
        public string CostSavingTip { get; set; } = string.Empty;
    }

    public class CropRotationPlan
    {
        public string KharifCrop { get; set; } = string.Empty;
        public string RabiCrop { get; set; } = string.Empty;
        public string ZaidCrop { get; set; } = string.Empty;
        public string RotationBenefit { get; set; } = string.Empty;
        public string NitrogenBalance { get; set; } = "High Positive";
        public string SoilHealthStatus { get; set; } = "Optimum Fertility";
    }

    public class GujaratAgroDataService : IGujaratAgroDataService
    {
        private readonly List<DistrictAgroProfile> _districts;

        public GujaratAgroDataService()
        {
            _districts = InitializeDistricts();
        }

        public List<DistrictAgroProfile> GetAllDistricts() => _districts.OrderBy(d => d.Name).ToList();

        public DistrictAgroProfile? GetDistrict(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return _districts.First();
            return _districts.FirstOrDefault(d => d.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
                   ?? _districts.FirstOrDefault(d => d.Name.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase))
                   ?? _districts.First();
        }

        public SoilBenchmarkData ResolveSoilAndClimate(string districtName, string soilType, string waterSource, string waterQuality, string? previousCrop)
        {
            var dist = GetDistrict(districtName) ?? _districts.First();

            double n = dist.BaselineN;
            double p = dist.BaselineP;
            double k = dist.BaselineK;
            double temp = dist.AvgTempC;
            double hum = dist.AvgHumidity;
            double ph = dist.BaselinePh;
            double rain = dist.AvgRainfallMm;

            // Soil Type Modifiers
            string sType = (soilType ?? "black").ToLowerInvariant();
            switch (sType)
            {
                case "black": // Black Regur Soil
                    n += 15;
                    p += 2;
                    k += 25;
                    ph = Math.Max(ph, 7.4);
                    hum += 3;
                    break;
                case "loamy": // Goradu / Loamy Alluvial
                    n += 5;
                    p += 8;
                    k += 8;
                    ph = 6.8;
                    break;
                case "sandy": // Light Sandy Loam
                    n -= 25;
                    p -= 10;
                    k -= 12;
                    ph = Math.Min(ph, 7.2);
                    hum -= 8;
                    break;
                case "alluvial": // Coastal / Riverbed Clay
                    n += 10;
                    p += 6;
                    k += 15;
                    ph = Math.Max(ph, 7.6);
                    hum += 5;
                    break;
            }

            // Water Source Modifiers
            string wSource = (waterSource ?? "borewell").ToLowerInvariant();
            if (wSource == "borewell")
            {
                rain = Math.Max(rain, 350.0);
            }
            else if (wSource == "canal")
            {
                rain = Math.Max(rain, 550.0);
                ph = 6.9; // Canal water balances soil alkalinity
            }
            else // Rainfed
            {
                rain = dist.AvgRainfallMm * 0.85; // Natural dryland condition
            }

            // Water Quality Modifiers
            string wQual = (waterQuality ?? "fresh").ToLowerInvariant();
            if (wQual == "saline")
            {
                ph += 0.4;
                k += 8; // Saline waters carry potassium salts
            }

            // Previous Crop Enrichment
            if (!string.IsNullOrWhiteSpace(previousCrop))
            {
                string prev = previousCrop.ToLowerInvariant();
                if (prev.Contains("groundnut") || prev.Contains("chana") || prev.Contains("pulse") || prev.Contains("moong") || prev.Contains("soybean"))
                {
                    n += 20; // Residual biological nitrogen fixation
                }
                else if (prev.Contains("cotton") || prev.Contains("sugarcane") || prev.Contains("banana"))
                {
                    n -= 15; // Heavy biomass depleted
                    k -= 10;
                }
            }

            // Keep within realistic botanical bounds
            n = Math.Max(15, Math.Min(180, n));
            p = Math.Max(10, Math.Min(140, p));
            k = Math.Max(10, Math.Min(180, k));
            ph = Math.Round(Math.Max(5.5, Math.Min(8.6, ph)), 1);
            temp = Math.Round(temp, 1);
            hum = Math.Round(Math.Max(20, Math.Min(95, hum)), 1);
            rain = Math.Round(Math.Max(50, rain), 0);

            return new SoilBenchmarkData
            {
                Nitrogen = Math.Round(n, 1),
                Phosphorus = Math.Round(p, 1),
                Potassium = Math.Round(k, 1),
                Temperature = temp,
                Humidity = hum,
                PhValue = ph,
                Rainfall = rain,
                DistrictName = dist.Name,
                SoilTypeName = char.ToUpper(sType[0]) + sType.Substring(1) + " Soil",
                WaterSummary = char.ToUpper(wSource[0]) + wSource.Substring(1) + (wQual == "saline" ? " (Semi-Saline)" : " (Sweet Water)")
            };
        }

        public FertilizerRecommendation CalculateFertilizer(string cropName, double n, double p, double k, double areaAcres)
        {
            // Standard ICAR Crop Nutrient Requirements (Target N, P, K in kg/acre)
            double targetN = 45;
            double targetP = 20;
            double targetK = 20;

            string c = (cropName ?? "Wheat").ToLowerInvariant();
            if (c.Contains("cotton")) { targetN = 65; targetP = 25; targetK = 25; }
            else if (c.Contains("groundnut")) { targetN = 15; targetP = 25; targetK = 20; }
            else if (c.Contains("wheat")) { targetN = 50; targetP = 25; targetK = 20; }
            else if (c.Contains("rice")) { targetN = 55; targetP = 25; targetK = 20; }
            else if (c.Contains("chana") || c.Contains("chickpea") || c.Contains("moong") || c.Contains("tuver")) { targetN = 10; targetP = 20; targetK = 10; }
            else if (c.Contains("cumin") || c.Contains("jeera")) { targetN = 20; targetP = 15; targetK = 15; }
            else if (c.Contains("castor")) { targetN = 40; targetP = 20; targetK = 15; }
            else if (c.Contains("mustard")) { targetN = 35; targetP = 20; targetK = 15; }
            else if (c.Contains("potato") || c.Contains("onion")) { targetN = 60; targetP = 35; targetK = 45; }
            else if (c.Contains("sugarcane") || c.Contains("banana")) { targetN = 90; targetP = 40; targetK = 60; }
            else if (c.Contains("pomegranate")) { targetN = 35; targetP = 25; targetK = 35; }

            // Calculate Deficiencies
            double defN = Math.Max(0, targetN - (n * 0.25));
            double defP = Math.Max(0, targetP - (p * 0.20));
            double defK = Math.Max(0, targetK - (k * 0.20));

            // Fertilizer Equivalents:
            // 1. DAP provides 46% P2O5 and 18% N
            double dapKg = Math.Round(defP / 0.46, 0);
            double nFromDap = dapKg * 0.18;

            // 2. Urea provides 46% N (remaining after DAP)
            double remN = Math.Max(0, defN - nFromDap);
            double ureaKg = Math.Round(remN / 0.46, 0);

            // 3. MOP (Muriate of Potash) provides 60% K2O
            double potashKg = Math.Round(defK / 0.60, 0);

            // Minimum practical doses
            if (ureaKg < 15 && !c.Contains("chana") && !c.Contains("groundnut")) ureaKg = 25;
            if (dapKg < 10) dapKg = 20;
            if (potashKg < 5 && (c.Contains("potato") || c.Contains("banana") || c.Contains("cotton"))) potashKg = 20;

            // 1 Acre = 2.4 Gujarat Vigha
            double ureaVigha = Math.Round(ureaKg / 2.4, 0);
            double dapVigha = Math.Round(dapKg / 2.4, 0);
            double potashVigha = Math.Round(potashKg / 2.4, 0);

            return new FertilizerRecommendation
            {
                UreaKgPerAcre = ureaKg,
                UreaKgPerVigha = ureaVigha,
                DapKgPerAcre = dapKg,
                DapKgPerVigha = dapVigha,
                PotashKgPerAcre = potashKg,
                PotashKgPerVigha = potashVigha,
                SowingBaseDose = $"Apply 100% DAP ({dapVigha} kg/vigha) + 100% Potash ({potashVigha} kg/vigha) + 30% Urea ({Math.Round(ureaVigha * 0.3, 0)} kg/vigha) as basal application at furrow sowing.",
                TopDressingStage1 = $"Apply 35% Urea ({Math.Round(ureaVigha * 0.35, 0)} kg/vigha) during first vegetative irrigation (20-30 days after germination).",
                TopDressingStage2 = $"Apply remaining 35% Urea ({Math.Round(ureaVigha * 0.35, 0)} kg/vigha) during flowering or pre-fruiting stage.",
                BioFertilizerAdvice = "Seed treatment with Trichoderma viride (10g/kg seed) and PSB (Phosphate Solubilizing Bacteria) to boost nutrient uptake by 25%.",
                CostSavingTip = "Soil test indicates balanced potassium. Avoid excess potash top-dressing to save Rs. 600 - 800 per acre."
            };
        }

        public CropRotationPlan GetCropRotationPlan(string primaryCrop)
        {
            string c = (primaryCrop ?? "Cotton").ToLowerInvariant();

            if (c.Contains("cotton"))
            {
                return new CropRotationPlan
                {
                    KharifCrop = "Cotton (Primary Cash Crop)",
                    RabiCrop = "Chickpea / Chana (Pulse - Nitrogen Restorer)",
                    ZaidCrop = "Green Mung / Sesame (Summer Soil Cover)",
                    RotationBenefit = "Chickpeas fix atmospheric nitrogen via root nodules, replenishing nutrients exhausted by heavy cotton feeding.",
                    NitrogenBalance = "+35 kg N/Acre Restored",
                    SoilHealthStatus = "Excellent & Pest Cycle Broken"
                };
            }
            if (c.Contains("groundnut"))
            {
                return new CropRotationPlan
                {
                    KharifCrop = "Groundnut (Nitrogen Enriched)",
                    RabiCrop = "Wheat / Cumin (High Value Rabi Crop)",
                    ZaidCrop = "Fodder Bajra / Cowpea",
                    RotationBenefit = "Groundnut leaves abundant organic nitrogen in soil, minimizing artificial urea requirement for winter wheat.",
                    NitrogenBalance = "+40 kg N/Acre Restored",
                    SoilHealthStatus = "Prime Organic Fertility"
                };
            }
            if (c.Contains("rice") || c.Contains("paddy"))
            {
                return new CropRotationPlan
                {
                    KharifCrop = "Rice / Paddy (Monsoon Moisture)",
                    RabiCrop = "Mustard (Rai) / Chickpea",
                    ZaidCrop = "Watermelon / Vegetables",
                    RotationBenefit = "Mustard deep roots aerate saturated clay soil and eradicate soil-borne fungal pathogens.",
                    NitrogenBalance = "+25 kg N/Acre Restored",
                    SoilHealthStatus = "Balanced Texture"
                };
            }
            if (c.Contains("cumin") || c.Contains("jeera"))
            {
                return new CropRotationPlan
                {
                    KharifCrop = "Castor / Pearl Millet (Bajra)",
                    RabiCrop = "Cumin (Jeera - Premium Spices)",
                    ZaidCrop = "Summer Fallow / Light Moong",
                    RotationBenefit = "Alternating cumin with pearl millet breaks wilt disease fungi (Fusarium oxysporum) in semi-arid soils.",
                    NitrogenBalance = "+20 kg N/Acre Restored",
                    SoilHealthStatus = "Disease Suppressive"
                };
            }
            if (c.Contains("potato") || c.Contains("onion"))
            {
                return new CropRotationPlan
                {
                    KharifCrop = "Groundnut / Green Manure (Dhaincha)",
                    RabiCrop = "Potato / Onion (Intensive Yield)",
                    ZaidCrop = "Pearl Millet (Bajra)",
                    RotationBenefit = "Summer green manuring with Dhaincha adds 15 tons/acre organic humus, drastically improving tuber size.",
                    NitrogenBalance = "+30 kg N/Acre Restored",
                    SoilHealthStatus = "Superior Tuber Bed"
                };
            }

            // Default balanced rotation
            return new CropRotationPlan
            {
                KharifCrop = $"{primaryCrop} (Kharif Main)",
                RabiCrop = "Chickpea / Mustard (Rabi Balancer)",
                ZaidCrop = "Green Gram (Moong)",
                RotationBenefit = "Legume-cereal alternating cycle preserves beneficial micro-flora and prevents soil salinization.",
                NitrogenBalance = "+28 kg N/Acre Restored",
                SoilHealthStatus = "Optimal Sustainable Health"
            };
        }

        private List<DistrictAgroProfile> InitializeDistricts()
        {
            return new List<DistrictAgroProfile>
            {
                // Saurashtra (JAU)
                new DistrictAgroProfile { Name = "Rajkot", Region = "Saurashtra", AgroClimaticZone = "North Saurashtra", AvgRainfallMm = 650, AvgTempC = 26.5, AvgHumidity = 62, BaselineN = 110, BaselineP = 42, BaselineK = 45, BaselinePh = 7.4, DefaultSoilType = "black", ProminentCrops = new() { "Groundnut", "Cotton", "Wheat", "Castor" } },
                new DistrictAgroProfile { Name = "Junagadh", Region = "Saurashtra", AgroClimaticZone = "South Saurashtra", AvgRainfallMm = 850, AvgTempC = 27.0, AvgHumidity = 68, BaselineN = 115, BaselineP = 45, BaselineK = 50, BaselinePh = 7.3, DefaultSoilType = "black", ProminentCrops = new() { "Groundnut", "Mango (Kesar)", "Cotton", "Wheat" } },
                new DistrictAgroProfile { Name = "Amreli", Region = "Saurashtra", AgroClimaticZone = "North Saurashtra", AvgRainfallMm = 600, AvgTempC = 27.2, AvgHumidity = 58, BaselineN = 105, BaselineP = 38, BaselineK = 40, BaselinePh = 7.5, DefaultSoilType = "black", ProminentCrops = new() { "Cotton", "Groundnut", "Sesame", "Onion" } },
                new DistrictAgroProfile { Name = "Bhavnagar", Region = "Saurashtra", AgroClimaticZone = "Bhal & Coastal", AvgRainfallMm = 580, AvgTempC = 27.8, AvgHumidity = 65, BaselineN = 100, BaselineP = 40, BaselineK = 48, BaselinePh = 7.6, DefaultSoilType = "black", ProminentCrops = new() { "Onion", "Cotton", "Groundnut", "Bajra" } },
                new DistrictAgroProfile { Name = "Jamnagar", Region = "Saurashtra", AgroClimaticZone = "North Saurashtra", AvgRainfallMm = 550, AvgTempC = 26.2, AvgHumidity = 66, BaselineN = 95, BaselineP = 36, BaselineK = 42, BaselinePh = 7.5, DefaultSoilType = "black", ProminentCrops = new() { "Groundnut", "Cotton", "Garlic", "Cumin" } },
                new DistrictAgroProfile { Name = "Morbi", Region = "Saurashtra", AgroClimaticZone = "North Saurashtra", AvgRainfallMm = 500, AvgTempC = 26.8, AvgHumidity = 58, BaselineN = 90, BaselineP = 35, BaselineK = 38, BaselinePh = 7.6, DefaultSoilType = "black", ProminentCrops = new() { "Cotton", "Groundnut", "Sesame", "Cumin" } },
                new DistrictAgroProfile { Name = "Surendranagar", Region = "Saurashtra", AgroClimaticZone = "North Saurashtra", AvgRainfallMm = 520, AvgTempC = 27.5, AvgHumidity = 52, BaselineN = 95, BaselineP = 34, BaselineK = 40, BaselinePh = 7.8, DefaultSoilType = "black", ProminentCrops = new() { "Cotton", "Cumin", "Sesame", "Chickpea" } },
                new DistrictAgroProfile { Name = "Gir Somnath", Region = "Saurashtra", AgroClimaticZone = "South Saurashtra", AvgRainfallMm = 900, AvgTempC = 27.0, AvgHumidity = 75, BaselineN = 120, BaselineP = 48, BaselineK = 52, BaselinePh = 7.2, DefaultSoilType = "black", ProminentCrops = new() { "Mango (Kesar)", "Groundnut", "Coconut", "Sugarcane" } },
                new DistrictAgroProfile { Name = "Botad", Region = "Saurashtra", AgroClimaticZone = "Bhal & Coastal", AvgRainfallMm = 560, AvgTempC = 27.0, AvgHumidity = 58, BaselineN = 100, BaselineP = 38, BaselineK = 44, BaselinePh = 7.5, DefaultSoilType = "black", ProminentCrops = new() { "Cotton", "Groundnut", "Wheat", "Onion" } },
                new DistrictAgroProfile { Name = "Porbandar", Region = "Saurashtra", AgroClimaticZone = "South Saurashtra", AvgRainfallMm = 680, AvgTempC = 26.5, AvgHumidity = 74, BaselineN = 105, BaselineP = 42, BaselineK = 46, BaselinePh = 7.4, DefaultSoilType = "black", ProminentCrops = new() { "Groundnut", "Cotton", "Wheat", "Castor" } },
                new DistrictAgroProfile { Name = "Devbhoomi Dwarka", Region = "Saurashtra", AgroClimaticZone = "North Saurashtra", AvgRainfallMm = 450, AvgTempC = 26.0, AvgHumidity = 72, BaselineN = 85, BaselineP = 32, BaselineK = 40, BaselinePh = 7.6, DefaultSoilType = "black", ProminentCrops = new() { "Groundnut", "Castor", "Sesame", "Cumin" } },

                // North Gujarat (SDAU)
                new DistrictAgroProfile { Name = "Banaskantha", Region = "North Gujarat", AgroClimaticZone = "North Gujarat", AvgRainfallMm = 520, AvgTempC = 25.8, AvgHumidity = 48, BaselineN = 65, BaselineP = 36, BaselineK = 55, BaselinePh = 7.1, DefaultSoilType = "sandy", ProminentCrops = new() { "Potato", "Mustard", "Castor", "Pomegranate" } },
                new DistrictAgroProfile { Name = "Patan", Region = "North Gujarat", AgroClimaticZone = "North Gujarat", AvgRainfallMm = 480, AvgTempC = 26.5, AvgHumidity = 50, BaselineN = 60, BaselineP = 32, BaselineK = 48, BaselinePh = 7.4, DefaultSoilType = "sandy", ProminentCrops = new() { "Cumin", "Castor", "Mustard", "Fennel" } },
                new DistrictAgroProfile { Name = "Mehsana", Region = "North Gujarat", AgroClimaticZone = "North Gujarat", AvgRainfallMm = 620, AvgTempC = 26.4, AvgHumidity = 55, BaselineN = 75, BaselineP = 38, BaselineK = 50, BaselinePh = 7.2, DefaultSoilType = "loamy", ProminentCrops = new() { "Fennel", "Mustard", "Castor", "Wheat" } },
                new DistrictAgroProfile { Name = "Sabarkantha", Region = "North Gujarat", AgroClimaticZone = "North Gujarat", AvgRainfallMm = 750, AvgTempC = 26.0, AvgHumidity = 58, BaselineN = 85, BaselineP = 40, BaselineK = 45, BaselinePh = 6.9, DefaultSoilType = "loamy", ProminentCrops = new() { "Wheat", "Maize", "Cotton", "Potato" } },
                new DistrictAgroProfile { Name = "Aravalli", Region = "North Gujarat", AgroClimaticZone = "North Gujarat", AvgRainfallMm = 780, AvgTempC = 25.9, AvgHumidity = 60, BaselineN = 85, BaselineP = 42, BaselineK = 46, BaselinePh = 6.8, DefaultSoilType = "loamy", ProminentCrops = new() { "Maize", "Wheat", "Cotton", "Groundnut" } },
                new DistrictAgroProfile { Name = "Gandhinagar", Region = "North Gujarat", AgroClimaticZone = "Middle Gujarat", AvgRainfallMm = 700, AvgTempC = 26.2, AvgHumidity = 60, BaselineN = 80, BaselineP = 40, BaselineK = 45, BaselinePh = 7.0, DefaultSoilType = "loamy", ProminentCrops = new() { "Wheat", "Vegetables", "Cotton", "Castor" } },
                new DistrictAgroProfile { Name = "Kutch", Region = "Kutch", AgroClimaticZone = "North West", AvgRainfallMm = 380, AvgTempC = 26.0, AvgHumidity = 52, BaselineN = 50, BaselineP = 28, BaselineK = 45, BaselinePh = 7.8, DefaultSoilType = "sandy", ProminentCrops = new() { "Pomegranate", "Castor", "Dates", "Cotton" } },

                // Central Gujarat (AAU)
                new DistrictAgroProfile { Name = "Anand", Region = "Central Gujarat", AgroClimaticZone = "Middle Gujarat", AvgRainfallMm = 820, AvgTempC = 26.5, AvgHumidity = 68, BaselineN = 95, BaselineP = 48, BaselineK = 45, BaselinePh = 6.7, DefaultSoilType = "loamy", ProminentCrops = new() { "Banana", "Rice", "Wheat", "Potato" } },
                new DistrictAgroProfile { Name = "Kheda", Region = "Central Gujarat", AgroClimaticZone = "Middle Gujarat", AvgRainfallMm = 800, AvgTempC = 26.6, AvgHumidity = 66, BaselineN = 92, BaselineP = 46, BaselineK = 44, BaselinePh = 6.8, DefaultSoilType = "loamy", ProminentCrops = new() { "Rice", "Wheat", "Potato", "Bajra" } },
                new DistrictAgroProfile { Name = "Ahmedabad", Region = "Central Gujarat", AgroClimaticZone = "Bhal & Coastal", AvgRainfallMm = 680, AvgTempC = 27.0, AvgHumidity = 60, BaselineN = 85, BaselineP = 42, BaselineK = 46, BaselinePh = 7.3, DefaultSoilType = "loamy", ProminentCrops = new() { "Bhalia Wheat", "Cotton", "Paddy", "Chickpea" } },
                new DistrictAgroProfile { Name = "Vadodara", Region = "Central Gujarat", AgroClimaticZone = "Middle Gujarat", AvgRainfallMm = 900, AvgTempC = 27.2, AvgHumidity = 68, BaselineN = 100, BaselineP = 46, BaselineK = 45, BaselinePh = 7.0, DefaultSoilType = "black", ProminentCrops = new() { "Cotton", "Banana", "Tur", "Sugarcane" } },
                new DistrictAgroProfile { Name = "Panchmahal", Region = "Central Gujarat", AgroClimaticZone = "Middle Gujarat", AvgRainfallMm = 920, AvgTempC = 26.8, AvgHumidity = 65, BaselineN = 85, BaselineP = 38, BaselineK = 38, BaselinePh = 6.8, DefaultSoilType = "loamy", ProminentCrops = new() { "Maize", "Paddy", "Pigeonpea", "Wheat" } },
                new DistrictAgroProfile { Name = "Dahod", Region = "Central Gujarat", AgroClimaticZone = "Middle Gujarat", AvgRainfallMm = 880, AvgTempC = 26.2, AvgHumidity = 62, BaselineN = 80, BaselineP = 36, BaselineK = 35, BaselinePh = 6.7, DefaultSoilType = "loamy", ProminentCrops = new() { "Maize", "Wheat", "Chickpea", "Soybean" } },
                new DistrictAgroProfile { Name = "Mahisagar", Region = "Central Gujarat", AgroClimaticZone = "Middle Gujarat", AvgRainfallMm = 850, AvgTempC = 26.5, AvgHumidity = 64, BaselineN = 85, BaselineP = 40, BaselineK = 40, BaselinePh = 6.8, DefaultSoilType = "loamy", ProminentCrops = new() { "Maize", "Paddy", "Wheat", "Pulses" } },
                new DistrictAgroProfile { Name = "Chhota Udepur", Region = "Central Gujarat", AgroClimaticZone = "Middle Gujarat", AvgRainfallMm = 950, AvgTempC = 27.0, AvgHumidity = 68, BaselineN = 90, BaselineP = 42, BaselineK = 38, BaselinePh = 6.9, DefaultSoilType = "black", ProminentCrops = new() { "Cotton", "Maize", "Pigeonpea", "Paddy" } },

                // South Gujarat (NAU)
                new DistrictAgroProfile { Name = "Surat", Region = "South Gujarat", AgroClimaticZone = "South Gujarat", AvgRainfallMm = 1200, AvgTempC = 27.5, AvgHumidity = 78, BaselineN = 110, BaselineP = 52, BaselineK = 45, BaselinePh = 6.8, DefaultSoilType = "black", ProminentCrops = new() { "Sugarcane", "Paddy", "Banana", "Vegetables" } },
                new DistrictAgroProfile { Name = "Bharuch", Region = "South Gujarat", AgroClimaticZone = "South Gujarat", AvgRainfallMm = 950, AvgTempC = 27.4, AvgHumidity = 72, BaselineN = 120, BaselineP = 48, BaselineK = 40, BaselinePh = 7.4, DefaultSoilType = "black", ProminentCrops = new() { "Cotton", "Banana", "Sugarcane", "Paddy" } },
                new DistrictAgroProfile { Name = "Navsari", Region = "South Gujarat", AgroClimaticZone = "South Gujarat Heavy Rainfall", AvgRainfallMm = 1500, AvgTempC = 27.0, AvgHumidity = 82, BaselineN = 100, BaselineP = 50, BaselineK = 42, BaselinePh = 6.5, DefaultSoilType = "alluvial", ProminentCrops = new() { "Sugarcane", "Mango (Alphonso)", "Paddy", "Sapota" } },
                new DistrictAgroProfile { Name = "Valsad", Region = "South Gujarat", AgroClimaticZone = "South Gujarat Heavy Rainfall", AvgRainfallMm = 1800, AvgTempC = 26.8, AvgHumidity = 84, BaselineN = 95, BaselineP = 48, BaselineK = 40, BaselinePh = 6.2, DefaultSoilType = "alluvial", ProminentCrops = new() { "Mango (Kesar/Alphonso)", "Paddy", "Sugarcane", "Cashew" } },
                new DistrictAgroProfile { Name = "Narmada", Region = "South Gujarat", AgroClimaticZone = "South Gujarat", AvgRainfallMm = 1100, AvgTempC = 27.2, AvgHumidity = 72, BaselineN = 105, BaselineP = 46, BaselineK = 42, BaselinePh = 6.9, DefaultSoilType = "black", ProminentCrops = new() { "Banana", "Sugarcane", "Cotton", "Paddy" } },
                new DistrictAgroProfile { Name = "Tapi", Region = "South Gujarat", AgroClimaticZone = "South Gujarat Heavy Rainfall", AvgRainfallMm = 1350, AvgTempC = 27.0, AvgHumidity = 78, BaselineN = 95, BaselineP = 45, BaselineK = 40, BaselinePh = 6.6, DefaultSoilType = "black", ProminentCrops = new() { "Sugarcane", "Paddy", "Banana", "Pulses" } },
                new DistrictAgroProfile { Name = "Dang", Region = "South Gujarat", AgroClimaticZone = "South Gujarat Heavy Rainfall", AvgRainfallMm = 2100, AvgTempC = 24.5, AvgHumidity = 85, BaselineN = 85, BaselineP = 40, BaselineK = 38, BaselinePh = 6.0, DefaultSoilType = "alluvial", ProminentCrops = new() { "Ragi / Nagli", "Paddy", "Horticulture", "Ginger" } }
            };
        }
    }
}
