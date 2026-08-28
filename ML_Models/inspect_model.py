import joblib
import os
import sys

model_path = os.path.join(os.path.dirname(__file__), 'crop_price_model.pkl')
try:
    model = joblib.load(model_path)
    print("MODEL_TYPE:", type(model))
    if hasattr(model, 'n_features_in_'):
        print("N_FEATURES:", model.n_features_in_)
    if hasattr(model, 'feature_names_in_'):
        print("FEATURE_NAMES:", list(model.feature_names_in_)[:20])
except Exception as e:
    print("ERROR:", e)
