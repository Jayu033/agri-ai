import os
import sys
import json
import warnings
import numpy as np
import pandas as pd

warnings.filterwarnings("ignore")

def predict_crop(N, P, K, temperature, humidity, ph, rainfall):
    model_path = os.path.join(os.path.dirname(__file__), 'crop_recommendation_model.pkl')
    try:
        import joblib
        model = joblib.load(model_path)
        
        # Build 1-row DataFrame
        df_input = pd.DataFrame([{
            'N': float(N),
            'P': float(P),
            'K': float(K),
            'temperature': float(temperature),
            'humidity': float(humidity),
            'ph': float(ph),
            'rainfall': float(rainfall)
        }])
        
        pred = model.predict(df_input)[0]
        
        # Calculate probabilities and alternatives
        if hasattr(model, "predict_proba"):
            probs = model.predict_proba(df_input)[0]
            classes = model.classes_
            top_indices = np.argsort(probs)[::-1]
            
            top_crop = classes[top_indices[0]]
            top_conf = round(float(probs[top_indices[0]]) * 100, 1)
            
            alternatives = []
            for idx in top_indices[1:4]:
                if probs[idx] > 0.01:
                    alternatives.append({
                        "crop": str(classes[idx]).capitalize(),
                        "score": round(float(probs[idx]) * 100, 1)
                    })
        else:
            top_crop = pred
            top_conf = 95.0
            alternatives = []

        return {
            "success": True,
            "crop": str(top_crop).capitalize(),
            "confidence": f"{top_conf}%",
            "source": "RandomForest Crop Classifier (crop_recommendation_model.pkl)",
            "alternatives": alternatives
        }
    except Exception as e:
        return {
            "success": False,
            "error": str(e)
        }

if __name__ == "__main__":
    n = float(sys.argv[1]) if len(sys.argv) > 1 else 90.0
    p = float(sys.argv[2]) if len(sys.argv) > 2 else 42.0
    k = float(sys.argv[3]) if len(sys.argv) > 3 else 43.0
    temp = float(sys.argv[4]) if len(sys.argv) > 4 else 20.8
    hum = float(sys.argv[5]) if len(sys.argv) > 5 else 82.0
    ph_val = float(sys.argv[6]) if len(sys.argv) > 6 else 6.5
    rain = float(sys.argv[7]) if len(sys.argv) > 7 else 200.0

    result = predict_crop(n, p, k, temp, hum, ph_val, rain)
    print(json.dumps(result, indent=2))
