import os
import pandas as pd
import numpy as np
from sklearn.model_selection import train_test_split
from sklearn.ensemble import RandomForestClassifier
from sklearn.metrics import accuracy_score, classification_report
import joblib

# 1. Load Crop Recommendation Dataset
csv_path = os.path.join(os.path.dirname(__file__), 'Crop_recommendation.csv')
df = pd.read_csv(csv_path)

print("Dataset Shape:", df.shape)
print("Features:", df.columns.tolist())
print("Unique Crops:", df['label'].unique().tolist())

# 2. Features (X) & Target (y)
X = df[['N', 'P', 'K', 'temperature', 'humidity', 'ph', 'rainfall']]
y = df['label']

# 3. Train Test Split
X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)

# 4. Train Random Forest Classifier
model = RandomForestClassifier(n_estimators=100, random_state=42)
model.fit(X_train, y_train)

# 5. Evaluate
y_pred = model.predict(X_test)
acc = accuracy_score(y_test, y_pred)
print(f"\nModel Accuracy: {acc * 100:.2f}%")

# 6. Save Model
model_save_path = os.path.join(os.path.dirname(__file__), 'crop_recommendation_model.pkl')
joblib.dump(model, model_save_path)
print(f"Saved model to: {model_save_path}")

# 7. Test sample inference
sample_input = np.array([[90, 42, 43, 20.8, 82.0, 6.5, 202.9]])
predicted_crop = model.predict(sample_input)[0]
proba = np.max(model.predict_proba(sample_input)) * 100
print(f"\nTest Prediction for [N=90, P=42, K=43, Temp=20.8, Humidity=82, pH=6.5, Rain=202.9]:")
print(f"Predicted Crop: {predicted_crop.upper()} ({proba:.1f}% confidence)")
