import os
import pandas as pd
import numpy as np
from sklearn.model_selection import train_test_split
from sklearn.ensemble import RandomForestRegressor
from sklearn.metrics import r2_score, mean_absolute_error
import joblib

# 1. Generate / Compile Comprehensive Indian & Gujarat APMC Market Price Dataset
crops_data = [
    # CropName, Category, BaseMSP, BasePrice, Season, GujaratDistrict
    ("Wheat", "Cereals", 2425, 2650, "Rabi", ["Rajkot", "Ahmedabad", "Bhavnagar", "Mehsana", "Junagadh"]),
    ("Paddy (Rice)", "Cereals", 2369, 2450, "Kharif", ["Ahmedabad", "Surat", "Kheda", "Vadodara", "Navsari"]),
    ("Bajra (Pearl Millet)", "Cereals", 2775, 2550, "Kharif", ["Banaskantha", "Patan", "Mehsana", "Kutch", "Bhavnagar"]),
    ("Maize (Corn)", "Cereals", 2400, 2250, "Kharif", ["Panchmahal", "Dahod", "Sabarkantha", "Vadodara"]),
    ("Jowar (Sorghum)", "Cereals", 3699, 4200, "Kharif", ["Surat", "Bharuch", "Rajkot", "Amreli"]),
    ("Barley (Jau)", "Cereals", 1980, 2300, "Rabi", ["Banaskantha", "Patan", "Mehsana"]),
    
    ("Cotton (Kapas)", "Fibre Crops", 7710, 7650, "Kharif", ["Rajkot", "Surendranagar", "Amreli", "Bhavnagar", "Botad", "Junagadh"]),
    ("Sugarcane (Serdi)", "Cash Crops", 3400, 3850, "Kharif", ["Surat", "Navsari", "Gir Somnath", "Valsad"]),
    ("Cumin (Jeera)", "Spices", 25000, 28500, "Rabi", ["Mehsana", "Patan", "Banaskantha", "Surendranagar", "Rajkot"]),
    ("Fennel (Variyali)", "Spices", 12000, 15500, "Rabi", ["Mehsana", "Patan", "Banaskantha", "Gandhinagar"]),
    ("Isabgol", "Spices", 14000, 16800, "Rabi", ["Mehsana", "Patan", "Banaskantha", "Kutch"]),
    ("Garlic (Lasan)", "Spices", 10000, 13500, "Rabi", ["Rajkot", "Jamnagar", "Junagadh", "Amreli"]),
    ("Chilli (Mirchi)", "Spices", 15000, 19200, "Kharif", ["Surat", "Vadodara", "Kheda", "Rajkot"]),
    ("Castor (Divela)", "Oilseeds", 5800, 6150, "Kharif", ["Banaskantha", "Patan", "Sabarkantha", "Mehsana"]),
    
    ("Groundnut (Magfali)", "Oilseeds", 7263, 6450, "Kharif", ["Rajkot", "Junagadh", "Amreli", "Jamnagar", "Bhavnagar", "Gir Somnath"]),
    ("Mustard (Raydo)", "Oilseeds", 5950, 6100, "Rabi", ["Banaskantha", "Patan", "Mehsana", "Sabarkantha"]),
    ("Sesamum (Til)", "Oilseeds", 9846, 12200, "Kharif", ["Amreli", "Bhavnagar", "Surendranagar", "Rajkot"]),
    ("Soybean", "Oilseeds", 5328, 4750, "Kharif", ["Dahod", "Panchmahal", "Vadodara", "Sabarkantha"]),
    
    ("Bengal Gram (Chana)", "Pulses", 5650, 5750, "Rabi", ["Rajkot", "Ahmedabad", "Patan", "Bhavnagar", "Surendranagar"]),
    ("Red Gram (Tuver)", "Pulses", 8000, 7450, "Kharif", ["Vadodara", "Bharuch", "Surat", "Panchmahal"]),
    ("Green Gram (Moong)", "Pulses", 8768, 7900, "Kharif", ["Kutch", "Banaskantha", "Surendranagar", "Ahmedabad"]),
    ("Black Gram (Urad)", "Pulses", 7800, 7100, "Kharif", ["Dahod", "Panchmahal", "Vadodara", "Sabarkantha"]),
    ("Lentil (Masur)", "Pulses", 6700, 7800, "Rabi", ["Banaskantha", "Patan", "Mehsana"]),
    
    ("Onion (Dungri)", "Vegetables", 1800, 2350, "Rabi", ["Bhavnagar", "Rajkot", "Junagadh", "Ahmedabad"]),
    ("Potato (Bataka)", "Vegetables", 1500, 1750, "Rabi", ["Banaskantha", "Sabarkantha", "Gandhinagar", "Kheda"]),
    ("Tomato", "Vegetables", 1600, 2200, "Kharif", ["Kheda", "Vadodara", "Surat", "Ahmedabad"]),
    ("Watermelon", "Fruits", 1200, 1850, "Zaid", ["Surat", "Kheda", "Ahmedabad", "Vadodara", "Bhavnagar"]),
    ("Banana", "Fruits", 1400, 1700, "Kharif", ["Bharuch", "Surat", "Narmada", "Vadodara"]),
    ("Mango (Keri)", "Fruits", 4000, 5200, "Summer", ["Gir Somnath", "Valsad", "Navsari", "Junagadh", "Kutch"]),
    ("Papaya", "Fruits", 1500, 2100, "Kharif", ["Vadodara", "Kheda", "Surat", "Anand"]),
    ("Pomegranate", "Fruits", 6000, 8500, "Kharif", ["Banaskantha", "Kutch", "Bhavnagar", "Ahmedabad"])
]

rows = []
np.random.seed(42)

for year in [2021, 2022, 2023, 2024, 2025, 2026]:
    year_trend = 1.0 + (year - 2021) * 0.045
    for crop, cat, msp, base_price, default_season, dist_list in crops_data:
        adjusted_msp = round(msp * (1.0 + (year - 2021) * 0.05), 2)
        for dist in dist_list:
            for season in ["Kharif", "Rabi", "Zaid"]:
                season_mult = 1.05 if season == default_season else 0.96
                for sample in range(4):
                    rainfall = np.random.uniform(30.0, 350.0)
                    rain_effect = 1.0 + ((rainfall - 150.0) / 1000.0)
                    area = np.random.uniform(1.0, 10.0)
                    
                    price_noise = np.random.normal(0, base_price * 0.04)
                    market_price = max(adjusted_msp * 0.85, (base_price * year_trend * season_mult * rain_effect) + price_noise)
                    market_price = round(market_price, 2)
                    
                    arrival_mt = round(np.random.uniform(500, 50000), 2)
                    
                    rows.append({
                        "Year": year,
                        "Crop": crop,
                        "Category": cat,
                        "Season": season,
                        "District": dist,
                        "State": "Gujarat",
                        "Rainfall_mm": round(rainfall, 1),
                        "Area_Hectares": round(area, 1),
                        "MSP": adjusted_msp,
                        "Arrival_MT": arrival_mt,
                        "Price_Per_Quintal": market_price
                    })

df_all = pd.DataFrame(rows)
csv_save_path = os.path.join(os.path.dirname(__file__), 'Indian_Gujarat_APMC_Prices.csv')
df_all.to_csv(csv_save_path, index=False)
print(f"Generated Comprehensive APMC Dataset: {df_all.shape} at {csv_save_path}")

# 2. Prepare Features & Target
X = df_all[['Crop', 'Season', 'District', 'State', 'Rainfall_mm', 'Area_Hectares', 'Year', 'MSP']]
y = df_all['Price_Per_Quintal']

X_encoded = pd.get_dummies(X, columns=['Crop', 'Season', 'District', 'State'], drop_first=False)
feature_names = X_encoded.columns.tolist()

X_train, X_test, y_train, y_test = train_test_split(X_encoded, y, test_size=0.2, random_state=42)

# 3. Train Random Forest Regressor
print("Training Multi-Crop Random Forest Regressor...")
rf = RandomForestRegressor(n_estimators=150, max_depth=25, random_state=42, n_jobs=-1)
rf.fit(X_train, y_train)

# 4. Evaluate
y_pred = rf.predict(X_test)
r2 = r2_score(y_test, y_pred) * 100
mae = mean_absolute_error(y_test, y_pred)

print(f"\n======================================")
print(f"Model R2 Score (Accuracy): {r2:.2f}%")
print(f"Mean Absolute Error (MAE): Rs. {mae:.2f} / Quintal")
print(f"======================================")

# 5. Save Model and Feature Names
model_path = os.path.join(os.path.dirname(__file__), 'crop_price_model.pkl')
features_path = os.path.join(os.path.dirname(__file__), 'model_features.pkl')

joblib.dump(rf, model_path)
joblib.dump(feature_names, features_path)
print(f"Saved Model to: {model_path}")
print(f"Saved Features ({len(feature_names)}) to: {features_path}")
