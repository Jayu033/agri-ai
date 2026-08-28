import joblib
import os

model_path = os.path.join(os.path.dirname(__file__), 'crop_price_model.pkl')
model = joblib.load(model_path)

all_features = list(model.feature_names_in_)
crop_cols = [f for f in all_features if any(k in f.lower() for k in ['crop', 'commodity'])]
state_cols = [f for f in all_features if 'state' in f.lower()]

print("Total features:", len(all_features))
print("Sample crop columns:", crop_cols[:30])
print("Sample state columns:", state_cols[:10])
